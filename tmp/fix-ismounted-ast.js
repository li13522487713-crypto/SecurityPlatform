const fs = require('fs');
const path = require('path');
const { parse, compileScript } = require(path.resolve('src/frontend/Atlas.WebApp/node_modules/@vue/compiler-sfc'));
const { Project, SyntaxKind } = require(path.resolve('src/frontend/Atlas.WebApp/node_modules/ts-morph'));

const filesTxt = fs.readFileSync('tmp/suspicious_files.txt', 'utf8');
const files = filesTxt.split('\n').filter(Boolean).map(f => f.trim());

const project = new Project({ useInMemoryFileSystem: true });

let modifiedCount = 0;

for (const file of files) {
  try {
    const rawContent = fs.readFileSync(file, 'utf8');
    const { descriptor } = parse(rawContent, { filename: file });
    if (!descriptor.scriptSetup) continue;
    
    const scriptContent = descriptor.scriptSetup.content;
    const sourceFile = project.createSourceFile('temp.ts', scriptContent, { overwrite: true });
    
    let needsIsMounted = false;
    let hasIsMounted = false;
    
    // Check if the script has onMounted with async awaits
    const onMounteds = sourceFile.getDescendantsOfKind(SyntaxKind.CallExpression)
      .filter(c => c.getExpression().getText() === 'onMounted');
      
    const awaits = sourceFile.getDescendantsOfKind(SyntaxKind.AwaitExpression);
    
    if (awaits.length === 0) continue;
    
    if (sourceFile.getText().includes('const isMounted = ref')) {
      hasIsMounted = true;
    }
    
    if (hasIsMounted) continue;
    
    // 1. AST injection: add isMounted import if missing
    let importDecl = sourceFile.getImportDeclaration(decl => decl.getModuleSpecifierValue() === 'vue');
    if (!importDecl) {
      sourceFile.addImportDeclaration({
        namedImports: ['ref', 'onMounted', 'onUnmounted'],
        moduleSpecifier: 'vue'
      });
    } else {
      const namedImports = importDecl.getNamedImports().map(n => n.getName());
      if (!namedImports.includes('ref')) importDecl.addNamedImport('ref');
      if (!namedImports.includes('onMounted')) importDecl.addNamedImport('onMounted');
      if (!namedImports.includes('onUnmounted')) importDecl.addNamedImport('onUnmounted');
    }
    
    // 2. Add isMounted definition
    sourceFile.insertStatements(importDecl ? importDecl.getChildIndex() + 1 : 0, 
      `\nconst isMounted = ref(false);\n` +
      `onMounted(() => { isMounted.value = true; });\n` + 
      `onUnmounted(() => { isMounted.value = false; });\n`
    );
    
    // 3. Add checks after awaits
    // Wait, adding statements via AST can be complex because of block boundaries.
    // Instead of deep AST manipulations for EVERY await, let's just do a textual replacement 
    // on the new script content using regex now that we safely injected the header.
    let updatedScript = sourceFile.getFullText();
    
    // Safe textual injection after basic await assignments
    updatedScript = updatedScript.replace(/^(\s*)(const|let|var)\s+([^=]+)\s*=\s*await\s+([^;]+);/gm, 
      '$1$2 $3 = await $4;\n$1if (!isMounted.value) return;'
    );
    updatedScript = updatedScript.replace(/^(\s*)([\w\.]+)\s*=\s*await\s+([^;]+);/gm, 
      '$1$2 = await $3;\n$1if (!isMounted.value) return;'
    );
    updatedScript = updatedScript.replace(/^(\s*)await\s+([^;]+);/gm, 
      '$1await $2;\n$1if (!isMounted.value) return;'
    );

    // Reconstruct the Vue file
    const startOffset = descriptor.scriptSetup.loc.start.offset;
    const endOffset = descriptor.scriptSetup.loc.end.offset;
    
    const newFileContent = rawContent.slice(0, startOffset) + '\n' + updatedScript.trim() + '\n' + rawContent.slice(endOffset);
    
    fs.writeFileSync(file, newFileContent);
    modifiedCount++;
    console.log(`Patched ${file}`);
    
  } catch (e) {
    console.error(`Failed on ${file}:`, e.message);
  }
}

console.log(`Total files patched: ${modifiedCount}`);

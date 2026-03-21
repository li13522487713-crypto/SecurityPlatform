const fs = require('fs');

const cs = fs.readFileSync('src/backend/Atlas.Infrastructure/Services/DatabaseInitializerHostedService.cs', 'utf8');

// Find all menu paths in CS
const regex = /\(\"([^\"]+)\",\s*\"([^\"]+)\",\s*(\"[^\"]+\"|null),\s*\d+,\s*\"[CMF]\"/g;
const matches = [...cs.matchAll(regex)];

const ts = fs.readFileSync('src/frontend/Atlas.WebApp/src/router/index.ts', 'utf8');
const feRegex = /path:\s*\"([^\"]+)\"/g;
const fePaths = [...ts.matchAll(feRegex)].map(m => m[1]);

const notFoundMap = [];
for (let m of matches) {
   let name = m[1];
   let p = m[2];
   let found = false;
   if (fePaths.includes(p)) found = true;
   else {
       // try matching dynamic like /ai/agents/:id/edit
       let paramRegex = new RegExp('^' + p.replace(/:[^\/]+/g, '[^/]+') + '$');
       for (let fp of fePaths) {
          if (new RegExp('^' + fp.replace(/:[^\/]+/g, '[^/]+') + '$').test(p)) { found = true; break; }
       }
   }
   if (!found && !p.includes(':query') && !p.includes(':create') && !p.includes(':update') && !p.includes(':delete') && !p.includes(':publish') && !p.includes(':debug') && !p.includes(':manage') && !p.includes(':run')) {
      notFoundMap.push({ name, path: p });
   }
}

console.log('Orphaned Seeded Menu Paths:', notFoundMap);

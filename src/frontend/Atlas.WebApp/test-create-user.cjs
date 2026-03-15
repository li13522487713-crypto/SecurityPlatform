const http = require('http');

async function run() {
  const tenantId = '00000000-0000-0000-0000-000000000001';
  
  // 1. Get Token
  const loginData = JSON.stringify({ username: 'admin', password: 'P@ssw0rd!' });
  const token = await new Promise((resolve, reject) => {
    const req = http.request({
      hostname: 'localhost', port: 5000, path: '/api/v1/auth/token', method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantId, 'Content-Length': Buffer.byteLength(loginData) }
    }, res => {
      let d = ''; res.on('data', c => d+=c); res.on('end', () => resolve(JSON.parse(d).data.accessToken));
    });
    req.write(loginData); req.end();
  });

  console.log("Got token:", !!token);

  // 2. Create User
  const userData = JSON.stringify({
    username: 'deptadmin.a.e2e.test2',
    name: '测试部门领导A',
    phoneNumber: '13800000099',
    password: 'P@ssw0rd!',
    gender: 1,
    accountType: 1,
    isEnabled: true
  });

  const reqUser = http.request({
    hostname: 'localhost', port: 5000, path: '/api/v1/users', method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
      'X-Tenant-Id': tenantId,
      'Content-Length': Buffer.byteLength(userData),
      'Idempotency-Key': 'test-create-uid-' + Date.now()
    }
  }, res => {
    let d = ''; res.on('data', c => d+=c);
    res.on('end', () => {
      require('fs').writeFileSync('result.txt', d, 'utf8');
      console.log(`CREATE USER STATUS: ${res.statusCode}`);
      console.log(`Saved body to result.txt`);
    });
  });
  reqUser.write(userData); reqUser.end();
}

run();

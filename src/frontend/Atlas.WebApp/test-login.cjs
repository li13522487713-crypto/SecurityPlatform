const http = require('http');

const postData = JSON.stringify({
  username: 'deptadmin.a.e2e',
  password: 'P@ssw0rd!'
});

const options = {
  hostname: 'localhost',
  port: 5000,
  path: '/api/v1/auth/token',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-Tenant-Id': '00000000-0000-0000-0000-000000000001',
    'Content-Length': Buffer.byteLength(postData)
  }
};

const req = http.request(options, (res) => {
  let data = '';
  res.on('data', (chunk) => {
    data += chunk;
  });
  res.on('end', () => {
    console.log(`STATUS: ${res.statusCode}`);
    console.log(`BODY: ${data}`);
  });
});

req.on('error', (e) => {
  console.error(`problem with request: ${e.message}`);
});

req.write(postData);
req.end();

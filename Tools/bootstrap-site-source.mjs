// Upload new or updated Site source in small Git pushes. Credentials are
// accepted through hidden stdin, kept in memory, and passed to Git via env.
import { spawn } from 'node:child_process';
import { readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
const credential=await new Promise((resolve,reject)=>{
  let input='';
  if(process.stdin.isTTY)process.stdin.setRawMode(true);
  process.stdin.setEncoding('utf8');
  process.stdin.resume();
  process.stdout.write('Ready for Site credential JSON on stdin (input is hidden).\n');
  process.stdin.on('data',data=>{
    input+=data;
    if(input.includes('\n')||input.includes('\r')){
      if(process.stdin.isTTY)process.stdin.setRawMode(false);
      process.stdin.pause();
      try{resolve(JSON.parse(input.trim()));}catch(error){reject(error);}
    }
  });
});
if(credential.auth_mode!=='http_extra_header'||!credential.remote_url||!credential.token)throw Error('Unsupported Site credential.');
async function git(args,network=false){
  const env={...process.env,GIT_TERMINAL_PROMPT:'0'};
  for(const key of Object.keys(env))if(key.startsWith('GIT_TRACE')||key==='GIT_CURL_VERBOSE')delete env[key];
  const auth=[];
  if(network){
    env.SITES_GIT_AUTHORIZATION='Authorization: Bearer '+credential.token;
    auth.push('-c','credential.helper=','-c','http.extraHeader=','-c','http.followRedirects=false',`--config-env=http.${credential.remote_url}.extraHeader=SITES_GIT_AUTHORIZATION`);
  }
  return await new Promise((resolve,reject)=>{
    const child=spawn('git',['-c','safe.directory='+process.cwd(),...auth,...args],{env,windowsHide:true,stdio:['ignore','pipe','pipe']});
    let out='',err='';child.stdout.on('data',s=>out+=s);child.stderr.on('data',s=>err+=s);
    child.on('error',reject);child.on('close',code=>code===0?resolve(out.trim()):reject(Error(err.split(credential.token).join('[redacted]'))));
  });
}
function files(directory='.'){
  return readdirSync(directory,{withFileTypes:true}).flatMap(entry=>entry.name==='.git'?[]:entry.isDirectory()?files(join(directory,entry.name)):[join(directory,entry.name)]);
}
try{
  const remote=await git(['ls-remote','--heads',credential.remote_url,'refs/heads/'+credential.branch],true);
  const oldHead=await git(['rev-parse','HEAD']);
  if(remote){
    if(remote.split(/\s+/)[0]!==oldHead)throw Error('Remote source differs from local HEAD; reconcile before uploading.');
    if(await git(['branch','--show-current'])!==credential.branch)throw Error('Use the configured Site branch.');
  }else{
    // Retain the previous unpublished commit before creating a small root commit.
    await git(['branch','source-before-bootstrap',oldHead]);
    await git(['symbolic-ref','HEAD','refs/heads/site-bootstrap']);
    await git(['read-tree','--empty']);
  }
  const all=files();
  const large=all.filter(file=>statSync(file).size>1024*1024);
  const small=all.filter(file=>!large.includes(file));
  const deleted=(await git(['ls-files','--deleted','-z'])).split('\0').filter(Boolean);
  if(small.length||deleted.length)await git(['add','--all','--',...small,...deleted]);
  if(await git(['diff','--cached','--name-only']))
    await git(['-c','user.name=Sites','-c','user.email=sites@users.noreply.openai.com','commit','-m','Update Vlliards browser entrypoint']);
  await git(['push',credential.remote_url,'HEAD:refs/heads/'+credential.branch],true);
  console.log('Site entrypoint source uploaded.');
  for(let i=0;i<large.length;i++){
    if(!await git(['status','--porcelain','--',large[i]]))continue;
    await git(['add','--',large[i]]);
    await git(['-c','user.name=Sites','-c','user.email=sites@users.noreply.openai.com','commit','-m',`Add game asset ${i+1} of ${large.length}`]);
    await git(['push',credential.remote_url,'HEAD:refs/heads/'+credential.branch],true);
    console.log(`Game asset ${i+1}/${large.length} uploaded.`);
  }
  if(!remote)await git(['branch','-M',credential.branch]);
  console.log(JSON.stringify({commit_sha:await git(['rev-parse','HEAD']),status:await git(['status','--porcelain'])}));
}catch(error){console.error(error.message.split(credential.token).join('[redacted]'));process.exitCode=1;}

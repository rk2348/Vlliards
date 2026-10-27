import { readFileSync, writeFileSync, mkdirSync, cpSync, readdirSync, statSync, unlinkSync, existsSync, realpathSync, rmSync } from 'node:fs';
import { join, resolve, sep } from 'node:path';
import { createHash } from 'node:crypto';
import './check-webgl-startup.mjs';
const build='Builds/WebGL', destination='Sites/vlliards-mobile/dist/play';
let html=readFileSync(join(build,'index.html'),'utf8');
const required=[...html.matchAll(/buildUrl \+ "\/([^"\n]+)"/g)].map(match=>match[1]);
if(required.length<4)throw Error('Could not determine required Unity build files.');
if(statSync(join(build,'index.html')).mtimeMs<statSync('Library/BuildPlayerData/Player/RuntimeInitializeOnLoads.json').mtimeMs)
  throw Error('Build artifacts predate the current player compilation; finish a successful WebGL build before exporting.');
const revision=createHash('sha256');
for(const name of required)revision.update(createHash('sha256').update(readFileSync(join(build,'Build',name))).digest());
const version=revision.digest('hex').slice(0,12);
// This is exclusively the generated game export. Verify its resolved target
// before removing old artifacts so stale releases are not re-uploaded.
const target=resolve(destination), workspace=resolve('.');
if(!target.startsWith(workspace+sep)||target!==resolve('Sites/vlliards-mobile/dist/play'))throw Error('Unsafe export target.');
if(existsSync(target)){
  if(realpathSync(target).toLowerCase()!==target.toLowerCase())throw Error('Refusing to replace a linked export directory.');
  rmSync(target,{recursive:true});
}
const buildDirectory='Build/'+version;
const outputBuild=join(destination,buildDirectory);
mkdirSync(outputBuild,{recursive:true});
for(const name of readdirSync(build)){
  if(name==='Build'||name==='index.html')continue;
  cpSync(join(build,name),join(destination,name),{recursive:true});
}
const map={};
for(const name of required){
  const bytes=readFileSync(join(build,'Build',name));
  const entry={bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex'),parts:[]};
  if(bytes.length>8*1024*1024){
    for(let offset=0,index=0;offset<bytes.length;offset+=8*1024*1024,index++){
      const part=name+'.part'+String(index).padStart(3,'0');
      writeFileSync(join(outputBuild,part),bytes.subarray(offset,offset+8*1024*1024));
      entry.parts.push(part);
    }
    const old=join(outputBuild,name);
    if(existsSync(old))unlinkSync(old);
  }else writeFileSync(join(outputBuild,name),bytes);
  map[name]=entry;
}
writeFileSync(join(destination,'asset-map.json'),JSON.stringify(map,null,2));
writeFileSync(join(destination,'build-path.json'),JSON.stringify({directory:buildDirectory,revision:version}));
const chunkLoader=`(()=>{
  const files=${JSON.stringify(map)};
  const originalFetch=window.fetch.bind(window);
  window.fetch=async function(input,options){
    const url=new URL(typeof input==='string'||input instanceof URL?input:input.url,document.baseURI);
    const file=files[url.pathname.split('/').pop()];
    if(url.origin!==location.origin||!url.pathname.includes('/Build/')||!file?.parts.length)return originalFetch(input,options);
    let index=0,reader=null;
    const stream=new ReadableStream({
      async pull(controller){
        try{
          while(true){
            if(!reader){
              if(index===file.parts.length){controller.close();return;}
              const response=await originalFetch(new URL(file.parts[index++],url).href,options);
              if(!response.ok)throw Error('Game data download failed: '+response.status);
              reader=response.body.getReader();
            }
            const next=await reader.read();
            if(next.done){reader=null;continue;}
            controller.enqueue(next.value);return;
          }
        }catch(error){controller.error(error);}
      },
      cancel(reason){return reader?.cancel(reason);}
    });
    const response=new Response(stream,{headers:{'Content-Type':'application/octet-stream','Content-Length':String(file.bytes)}});
    Object.defineProperty(response,'url',{value:url.href});
    return response;
  };
})();`;
writeFileSync(join(destination,'chunk-loader.js'),chunkLoader);
html=html.replace('</head>',`<style>html,body{margin:0;width:100%;height:100%;overflow:hidden}#unity-container{position:fixed!important;inset:0!important;transform:none!important;width:100%!important;height:100%!important}#unity-canvas{width:100%!important;height:100%!important;display:block;touch-action:none}#unity-footer{display:none}</style></head>`)
  .replace('</head>',`<script src="chunk-loader.js?v=${version}"></script></head>`)
  .replace('var buildUrl = "Build";',`var buildUrl = "${buildDirectory}";`)
  .replace('// config.matchWebGLToCanvasSize = false;','config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 1.5);');
writeFileSync(join(destination,'index.html'),html);
const landing='Sites/vlliards-mobile/dist/index.html';
writeFileSync(landing,readFileSync(landing,'utf8').replace(/game.src='play\/index\.html(?:\?v=[a-f0-9]+)?'/,`game.src='play/index.html?v=${version}'`));
const bytes=required.reduce((total,name)=>total+statSync(join(build,'Build',name)).size,0);
console.log(JSON.stringify({revision:version,files:required,downloadMB:Math.round(bytes/1024/1024*10)/10}));

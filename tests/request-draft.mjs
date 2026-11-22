import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {pathToFileURL} from 'node:url';
import {createRequire} from 'node:module';
import path from 'node:path';
import ts from '../frontend/node_modules/typescript/lib/typescript.js';
const require=createRequire(import.meta.url);
function moduleUrl(file,replacements={}) {
  let source=ts.transpileModule(readFileSync(file,'utf8'),{compilerOptions:{module:ts.ModuleKind.ESNext,target:ts.ScriptTarget.ES2022}}).outputText;
  for(const [from,to]of Object.entries(replacements))source=source.replaceAll(`from "${from}"`,`from "${to}"`);
  return 'data:text/javascript;base64,'+Buffer.from(source).toString('base64');
}
const options=pathToFileURL(path.resolve('frontend/lib/book-options.ts')).href;
const book=moduleUrl('frontend/lib/book-pricing.ts',{'./book-options':options});
const pricing=moduleUrl('frontend/lib/pricing.ts',{'./book-options':options,'./book-pricing':book});
const schema=moduleUrl('frontend/lib/print-spec.ts',{'./pricing':pricing,'./book-options':options,'zod':pathToFileURL(require.resolve('zod',{paths:[path.resolve('frontend')]})).href});
const {storeRequestSpec,readRequestSpec,clearRequestSpec}=await import(moduleUrl('frontend/lib/request-draft.ts',{'./print-spec':schema}));
const {defaultSpec}=await import(pricing);
const data=new Map(),storage={getItem:k=>data.get(k)??null,setItem:(k,v)=>data.set(k,v),removeItem:k=>data.delete(k)};
let checks=0;
function check(ok,message){assert.ok(ok,message);checks++;}
check(readRequestSpec(storage,1000)===null,'missing draft is safe');
storeRequestSpec(storage,{...defaultSpec,title:'private title',phone:'private phone',password:'private password',otp:'secret',registrationToken:'secret',notes:'private notes'},1000);
const raw=[...data.values()][0];
check(!/private|secret|password|phone|registrationToken|notes/.test(raw),'schema allowlist excludes personal fields, passwords, OTPs and grants');
check(JSON.stringify(readRequestSpec(storage,1001))===JSON.stringify(defaultSpec),'technical specification round-trips without recalculating prices');
check(readRequestSpec(storage,1000+2*60*60*1000+1)===null,'expired draft is never silently restored');
check(readRequestSpec(storage,999)===null,'future timestamp is rejected');
clearRequestSpec(storage);check(data.size===0,'successful request can explicitly clear the tab draft');
for(const value of ['not JSON','null','{"version":2,"savedAt":1000,"spec":{}}','{"version":1,"savedAt":1000,"spec":{"pages":-1}}']) {
  data.set('chap-print-spec-v1',value);check(readRequestSpec(storage,1001)===null,'malformed or incompatible draft is safe');
}
check(readRequestSpec({getItem(){throw Error('storage blocked');}},1000)===null,'blocked browser storage does not break rendering');
console.log(`${checks} request draft privacy/expiry/validation checks passed; no network or database.`);

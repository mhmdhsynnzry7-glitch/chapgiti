import {existsSync as releaseFileExists} from "node:fs";
import {DatabaseSync} from "node:sqlite";
import {existsSync} from "node:fs";
import path from "node:path";
import {environment,buildBackend,cli} from "./dotnet-runtime.mjs";
const development=process.env.NODE_ENV!=="production"&&!releaseFileExists("RELEASE-MANIFEST.json"),env=environment(development);
const file=path.resolve(process.argv[2]||path.join(env.Storage__Root,"site.sqlite"));
if(!existsSync(file))throw new Error("Source SQLite file was not found.");
if(development)await buildBackend(true);
const db=new DatabaseSync(file,{readOnly:true});
const tables=["settings","admin_users","admin_usernames","customer_registrations","customer_accounts","orders","events","files","articles","messages","sms_logs","audit_logs","banner_assets","tickets","ticket_messages"];
let snapshot;
try{
 db.exec("BEGIN");
 const existing=new Set(db.prepare("SELECT name FROM sqlite_master WHERE type='table'").all().map(row=>row.name));
 snapshot=Object.fromEntries(tables.map(table=>[table,existing.has(table)?db.prepare("SELECT * FROM "+table+" ORDER BY rowid").all():[]]));
 db.exec("COMMIT");
}finally{db.close();}
await cli(["--import-stdin"],env,{development,input:JSON.stringify(snapshot)});
console.log("Source SQLite database was opened read-only and was not modified.");

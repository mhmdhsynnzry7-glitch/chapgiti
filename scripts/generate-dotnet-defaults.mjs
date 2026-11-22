import ts from "../frontend/node_modules/typescript/lib/typescript.js";
import {readFileSync,writeFileSync,mkdirSync} from "node:fs";
import path from "node:path";
import {pathToFileURL} from "node:url";
const stage=path.resolve(".qa/dotnet-definitions");mkdirSync(stage,{recursive:true});
for(const name of ["book-options","book-pricing","pricing","content","banners","portfolio"]) {
 let source=readFileSync("frontend/lib/"+name+".ts","utf8");
 if(["banners","portfolio"].includes(name))source=source.replace(/^import .*$/gm,"");
 source=source.replace(/^import .*from ["']@\/runtime\/database.mjs["'];?$/gm,"const database = () => { throw new Error('Definitions only'); };");
 const compiled=ts.transpileModule(source,{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ES2022}}).outputText
   .replace(/(from ["']\.\/[^"']+)(["'])/g,(_all,prefix,quote)=>prefix.replace(/\.ts$/,"")+".mjs"+quote);
 writeFileSync(path.join(stage,name+".mjs"),compiled);
}
const load=name=>import(pathToFileURL(path.join(stage,name+".mjs")).href+"?t="+Date.now());
const options=await load("book-options"),pricing=await load("pricing"),content=await load("content"),banners=await load("banners"),portfolio=await load("portfolio");
const legacyRates=JSON.parse(readFileSync(new URL("./legacy-print-rates.json",import.meta.url),"utf8"));
const data={options,rates:legacyRates,settings:content.initialSettings,services:content.initialServices,articles:content.initialArticles,banners:banners.defaultBanners,portfolio:portfolio.defaultPortfolio,statuses:pricing.statuses,transitions:pricing.transitions};
if (!process.argv.includes("--reference-only")) writeFileSync("backend/ChapGiti.Infrastructure/Content/defaults.json",JSON.stringify(data,null,2));
console.log("Shared print definitions and default content generated for ASP.NET Core.");

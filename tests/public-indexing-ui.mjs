import assert from "node:assert/strict";

export async function publicIndexingChecks({page,base,check}) {
  const client=page.request, origin={Origin:base};
  const robots=await (await client.get(base+"/robots.txt")).text();
  for(const route of ["/account","/admin","/api/","/tickets","/order","/calculator","/customer","/track"]) {
    check(robots.includes("Disallow: "+route+"\n"),"robots protects "+route);
  }
  check(robots.includes("Sitemap: "+base+"/sitemap.xml"),"robots advertises canonical sitemap");
  assert((await client.post(base+"/api/admin/articles/prepare-drafts",{headers:origin})).ok());
  const adminList=await (await client.get(base+"/api/admin/articles?pageSize=50")).json();
  const row=adminList.items.find(a=>a.slug==="choose-book-trim");
  const endpoint=base+"/api/admin/articles/"+row.id;
  const draft=await (await client.get(endpoint)).json();
  let map=await (await client.get(base+"/sitemap.xml")).text();
  check(!map.includes("/blog/"+row.slug) && !map.includes("/calculator") && !map.includes("/admin"),"sitemap excludes draft article and restricted areas");
  assert((await client.put(endpoint,{headers:origin,data:{...draft,status:"published",authorName:"",coverAlt:"",coverAssetId:""}})).ok());
  map=await (await client.get(base+"/sitemap.xml")).text();
  check(map.includes("<loc>"+base+"/blog/"+row.slug+"</loc>"),"newly published article is present at its canonical URL");
  for(const path of ["/","/services","/blog","/blog/"+row.slug,"/contact"]) {
    const response=await page.goto(base+path);assert.equal(response.status(),200);
    const canonical=await page.locator('link[rel=canonical]').getAttribute("href");assert.equal(new URL(canonical).href,new URL(base+path).href);
    check(!(await page.locator('meta[name=robots]').getAttribute('content')).includes('noindex'),"public route is indexable with indexing enabled "+path);
  }
  await page.goto(base+"/calculator");
  check((await page.locator('meta[name=robots]').getAttribute('content')).includes("noindex"),"authenticated calculator remains private when public indexing is enabled");
}

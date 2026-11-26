import http from "node:http";

// The upstream is always a fixture on loopback. Never derive its address from a request.
export function forwardUpgrade(request, client, head, port) {
  const relay = http.request({hostname:"127.0.0.1",port,path:request.url,headers:request.headers});
  relay.on("upgrade", (response, upstream, initial) => {
    const headers = Object.entries(response.headers).map(([key, value]) => key + ": " + value);
    client.write(["HTTP/1.1 101 Switching Protocols", ...headers, "", ""].join("\r\n"));
    if (head.length) upstream.write(head);
    if (initial.length) client.write(initial);
    upstream.pipe(client);
    client.pipe(upstream);
    client.on("error", () => upstream.destroy());
    upstream.on("error", () => client.destroy());
    client.on("close", () => upstream.destroy());
    upstream.on("close", () => client.destroy());
  });
  relay.on("response", response => { response.resume(); client.destroy(); });
  relay.on("error", () => client.destroy());
  relay.end();
}

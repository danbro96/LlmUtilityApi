# LlmUtilityApi

A stateless .NET 10 **MCP tool server** for LLM agents. It exposes a catalog of deterministic utility
tools (math, time, text/regex, json, crypto, random) plus web-`search` (SearXNG), a web-`fetch` tool,
and document-text extraction, mounted over MCP Streamable HTTP. No UI, no database — pure/local
computation except the two outbound tools (`search`, `fetch`).

## Tools

| Group | Tools |
|---|---|
| math | `math_eval` (exact decimal, NCalc), `convert_unit` (UnitsNet) |
| time | `time_now`, `time_add`, `time_diff`, `time_parse`, `time_format`, `time_relative`, `cron_next` |
| text | `regex_test`, `regex_extract`, `regex_replace`, `diff` (DiffPlex), `template` (Scriban) |
| random | `random`, `dice` (`NdM±K`), `shuffle`, `sample` — all optionally seeded |
| json | `json_query` (JSONPath), `json_validate` (Schema 2020-12), `json_format` |
| crypto | `hash`, `hmac`, `encode`/`decode` (base64/base64url/hex/url), `jwt_decode` (no verify), `uuid` (v7), `ulid` |
| search | `web_search` — query → ranked `{title, url, snippet}` via a self-hosted SearXNG instance |
| fetch | `fetch_url` — http(s) page → readable article text (SmartReader) or raw; SSRF-guarded, size-capped |
| doc | `extract_text` (base64 PDF/docx/HTML/text), `chunk` (overlapping windows for embedding/RAG) |

## Surface

Agent **MCP** at `/mcp` (Streamable HTTP) plus `/openapi/v1.json`, `/scalar`, `/livez`, `/readyz`. The MCP
surface is **LAN/WireGuard-only** — not meant to be tunnelled publicly. Health probes are process-up only.

## Auth & exposure

`X-API-Key` header (or `?api_key=` for the MCP transport), constant-time matched against `Auth:ApiKeys[]`.
Two outbound paths: `fetch` reaches arbitrary user URLs behind an SSRF guard that refuses
loopback/private/link-local/CGNAT/cloud-metadata addresses (re-checked on each redirect) unless
`Fetch:AllowPrivateNetworks=true` — keep that **false** in any networked deployment. `search` calls only
the single trusted `Search:BaseUrl` (a self-hosted SearXNG, typically on the LAN), so it is not SSRF-guarded.

## Config

| Section | Key | Default | Notes |
|---|---|---|---|
| `Auth` | `ApiKeys[]` (`Key`, `Name`) | — | accepted `X-API-Key`s (≥1 required) |
| `Auth` | `AllowedOrigins[]` | empty | CORS off unless set |
| `RateLimit` | `RequestsPerMinute` | `120` | per-key token bucket; 429 over limit |
| `Fetch` | `MaxResponseBytes` / `TimeoutSeconds` / `MaxRedirects` | 5 MiB / 20 / 5 | `fetch_url` limits |
| `Fetch` | `AllowPrivateNetworks` | `false` | **keep false** — the SSRF guard |
| `Search` | `BaseUrl` | empty | SearXNG URL (e.g. `http://searxng:8080`); required for `web_search` |
| `Search` | `MaxResults` / `TimeoutSeconds` / `Language` | 10 / 15 / — | `web_search` result cap, timeout, optional lang |
| `Doc` | `MaxBytes` | 20 MiB | `extract_text` cap (also sizes the request body limit) |
| OTEL | `OTEL_EXPORTER_OTLP_ENDPOINT`, … | unset → off | OTLP export when set |

Env-var form uses `__` for nesting (e.g. `Fetch__MaxResponseBytes`, `Auth__ApiKeys__0__Key`). See
[deploy/.env.example](deploy/.env.example).

## Build & test

```bash
dotnet test LlmUtilityApi.slnx                       # unit (tools + IpGuard) — fast, no I/O
dotnet test tests/LlmUtilityApi.IntegrationTests     # HTTP/MCP smoke end-to-end
```

## Deploy

Built and pushed by CI to `danbro96/llm-utility-api`. The API runs as a single stateless container (no DB,
no volumes) with `/mcp` kept LAN-only, alongside an internal **SearXNG** sidecar (`web_search`'s backend;
its only state is a small `settings.yml` config folder). See [deploy/compose.yaml](deploy/compose.yaml) for
a genericized stack and [deploy/searxng/settings.yml](deploy/searxng/settings.yml) for the SearXNG config;
the live deploy/runbook lives in the private infra docs.

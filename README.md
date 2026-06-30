# LlmUtilityApi

A stateless .NET 10 **MCP tool server** for LLM agents. It exposes a catalog of deterministic utility
tools (math, time, text/regex, json, crypto, random) plus a web-`fetch` tool and document-text
extraction, mounted over MCP Streamable HTTP. No UI, no database — pure/local computation except the
SSRF-guarded `fetch`.

## Tools

| Group | Tools |
|---|---|
| math | `math_eval` (exact decimal, NCalc), `convert_unit` (UnitsNet) |
| time | `time_now`, `time_add`, `time_diff`, `time_parse`, `time_format`, `time_relative`, `cron_next` |
| text | `regex_test`, `regex_extract`, `regex_replace`, `diff` (DiffPlex), `template` (Scriban) |
| random | `random`, `dice` (`NdM±K`), `shuffle`, `sample` — all optionally seeded |
| json | `json_query` (JSONPath), `json_validate` (Schema 2020-12), `json_format` |
| crypto | `hash`, `hmac`, `encode`/`decode` (base64/base64url/hex/url), `jwt_decode` (no verify), `uuid` (v7), `ulid` |
| fetch | `fetch_url` — http(s) page → readable article text (SmartReader) or raw; SSRF-guarded, size-capped |
| doc | `extract_text` (base64 PDF/docx/HTML/text), `chunk` (overlapping windows for embedding/RAG) |

## Surface

Agent **MCP** at `/mcp` (Streamable HTTP) plus `/openapi/v1.json`, `/scalar`, `/livez`, `/readyz`. The MCP
surface is **LAN/WireGuard-only** — not meant to be tunnelled publicly. Health probes are process-up only.

## Auth & exposure

`X-API-Key` header (or `?api_key=` for the MCP transport), constant-time matched against `Auth:ApiKeys[]`.
The `fetch` tool is the only outbound path; its SSRF guard refuses loopback/private/link-local/CGNAT/cloud-metadata
addresses (re-checked on each redirect) unless `Fetch:AllowPrivateNetworks=true` — keep that **false** in any
networked deployment.

## Config

| Section | Key | Default | Notes |
|---|---|---|---|
| `Auth` | `ApiKeys[]` (`Key`, `Name`) | — | accepted `X-API-Key`s (≥1 required) |
| `Auth` | `AllowedOrigins[]` | empty | CORS off unless set |
| `RateLimit` | `RequestsPerMinute` | `120` | per-key token bucket; 429 over limit |
| `Fetch` | `MaxResponseBytes` / `TimeoutSeconds` / `MaxRedirects` | 5 MiB / 20 / 5 | `fetch_url` limits |
| `Fetch` | `AllowPrivateNetworks` | `false` | **keep false** — the SSRF guard |
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

Built and pushed by CI to `danbro96/llm-utility-api`. Runs as a single stateless container (no DB, no
volumes) with `/mcp` kept LAN-only. See [deploy/compose.yaml](deploy/compose.yaml) for a genericized
stack; the live deploy/runbook lives in the private infra docs.

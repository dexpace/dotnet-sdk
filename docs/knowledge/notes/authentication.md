# authentication — notes

Hand-written. `../harvested/authentication.md` is what the documents say; this is what the port decided, and it wins.

## Superseded
- **Hex is lower-cased with `Convert.ToHexStringLower`, because the port targets `net10.0` only.** Supersedes `authentication/af09917c`: design §6.3's "lower-cases hex output manually … the port targets net8.0" predates roadmap decision D1, which set every project to `net10.0`; the Digest code calls `Convert.ToHexStringLower` (verified to return `ab0f` for `[0xAB, 0x0F]`), and `.editorconfig`'s CA1308 relaxation is unrelated to it.
  <sub>review · `docs/work/mvp/phase6/phase6c/2026-10-08-phase6c-auth-checklist.md` · high · sha:manual-p6c-hex-lower</sub>

## Conclusions
- **`net10.0` has no `MD5.IsSupported`, so the Digest handler finds out whether MD5 is available by trying one hash, once per process.** Verified on 10.0.401 (reflection finds no such member, `MD5.HashData([])` succeeds); a `CryptographicException` or `PlatformNotSupportedException` means a FIPS-refusing provider, which drops the MD5 algorithms from the handler's effective preference.
  <sub>review · `docs/work/mvp/phase6/phase6c/2026-10-08-phase6c-auth-checklist.md` · high · sha:manual-p6c-md5-probe</sub>
- **`Uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)` equals the request-target `SocketsHttpHandler` writes, so it is the Digest `uri`.** Verified on the wire for `/a%2Fb?x=%26y`, an empty path (`/`), an empty path with a query (`/?q=1`) and `/p%20q/r?a=b%20c&d=%7E` (both write `d=~`); `DigestWireTests` pins it.
  <sub>review · `docs/work/mvp/phase6/phase6c/2026-10-08-phase6c-auth-checklist.md` · high · sha:manual-p6c-digest-uri</sub>

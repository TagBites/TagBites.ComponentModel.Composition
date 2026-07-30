# Security Policy

## Supported versions

| Version | Supported |
| --- | --- |
| 1.2.x | Yes |
| < 1.2 | No |

Security fixes go into the latest released minor version. Older versions receive no updates.

## Reporting a vulnerability

Report a vulnerability privately through [GitHub security advisories](https://github.com/TagBites/TagBites.ComponentModel.Composition/security/advisories/new). Do not use a public issue for a security report.

Include the affected version, a description of the problem and the steps to reproduce it. The maintainers answer within a few business days.

## Security model

The library keeps a registry of exported types and creates their instances on demand. It does not isolate the code it runs, and it does not check permissions. Two areas define the trust boundary.

### A loaded assembly is trusted code

`LoadAssembly` reads all types of an assembly, and `CreateInstance` runs the constructor of an exported type. The created code runs with the full permissions of the host process. There is no sandbox, no signature check and no allowlist of types.

Load only assemblies from a trusted source. Verify the origin of a third-party plugin, for example through a strong name or an Authenticode signature, before the assembly reaches `LoadAssembly`.

### The cache directory is part of the trust boundary

`UseCache` stores export definitions as files and reads them on the next start. A definition holds a contract name, a type name and an export URI. The container resolves these names and creates instances from the assembly that the file describes. A process that writes to the cache directory therefore controls which types the container creates, without touching a single assembly.

- Place the cache in a directory that only the application and an administrator can write. A directory writable by every local user is not safe.
- The caller provides the serializer. Use a serializer that reads plain data, such as `System.Text.Json` with a concrete target type. A serializer with polymorphic type handling, for example `BinaryFormatter` or `TypeNameHandling` in `Newtonsoft.Json`, turns a cache file into arbitrary code execution.
- `UseCustomTypeResolver` replaces the default `Type.GetType` resolution. A custom resolver that limits results to known assemblies reduces the reach of a modified cache file.
- A cache file holds names, never code. Deleting the cache directory is always safe. The container falls back to reflection and rebuilds the files.

## Out of scope

The following cases are not treated as vulnerabilities in this library.

- Behavior of a plugin that the application loaded on purpose, including a constructor with side effects, an exception during instantiation or high resource use.
- A cache directory that untrusted users can write to, when the application chose that location.
- Use of an unsafe serializer in the delegates passed to `UseCache`.

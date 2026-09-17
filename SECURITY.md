# Security

Report vulnerabilities privately through
[GitHub's vulnerability reporting form](https://github.com/Taifunov/HelperLib.Discord/security/advisories/new).
Include the affected version, reproduction steps, and impact. Do not include
live credentials or disclose exploitable details in a public issue.

## Trust boundaries

- Store Discord bot tokens outside source control. HelperLib.Discord accepts a token
  from the host application and does not provide a credential store.
- `SendImageAsync` only downloads HTTPS URLs on `cdn.discordapp.com` and
  `media.discordapp.net`, without redirects, with an 8 MiB response limit and
  a 30-second timeout. Hosts remain responsible for command authorization,
  rate limits, overall concurrent request limits, and network egress policy.
- Guild/role helpers use Discord's cached state. They do not replace application
  authorization or provide isolation between multiple bot clients in one process.
- The reconnect watchdog can terminate the process after unrecoverable failure.
  Run the bot under a supervisor if automatic restart is required.

Pull request CI runs with read-only repository permissions. Only manually
dispatched release workflows on the default branch can prepare or publish
releases; publishing uses NuGet Trusted Publishing.

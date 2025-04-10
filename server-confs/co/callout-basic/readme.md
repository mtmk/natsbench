# callout example

```shell
dotnet run --project CalloutSetup
nats-server -c server.conf &
dotnet run --project CalloutService &
dotnet run --project CalloutClient
```
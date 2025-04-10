using NATS.Client.Core;
using NATS.Jwt;
using NATS.Jwt.Models;
using NATS.Net;
using NATS.NKeys;
using Synadia.AuthCallout;

SetCurrentDirectoryToProjectRoot();

var kp = KeyPair.FromSeed(File.ReadAllText("account.nk"));
var jwt = new NatsJwt();

await using var connection = new NatsConnection(new NatsOpts{AuthOpts = new NatsAuthOpts{Username = "auth"}});
var opts = new NatsAuthServiceOpts(Authorizer, ResponseSigner)
{
    ErrorHandler = (error, ct) =>
    {
        Console.WriteLine(error);
        return ValueTask.CompletedTask;
    },
    EncryptionKey = null,
};
await using var service = new NatsAuthService(connection.CreateServicesContext(), opts);

await service.StartAsync();

Console.ReadLine();

return;

ValueTask<string> Authorizer(NatsAuthorizationRequest request, CancellationToken cancellationToken)
{
    if (request.NatsConnectOptions.Username != "let-me-in")
    {
        throw new Exception("can't let you in i'm afraid");
    }
    
    NatsUserClaims user = jwt.NewUserClaims(request.UserNKey);
    user.Audience = "$G";
    return ValueTask.FromResult(jwt.EncodeUserClaims(user, kp));
}

ValueTask<string> ResponseSigner(NatsAuthorizationResponseClaims response, CancellationToken cancellationToken)
{
    return ValueTask.FromResult(jwt.EncodeAuthorizationResponseClaims(response, kp));
}

static void SetCurrentDirectoryToProjectRoot()
{
    var d = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (!d.GetFiles(".project.root").Any())
    {
        d = d.Parent!;
    }
    Directory.SetCurrentDirectory(d.FullName);
}
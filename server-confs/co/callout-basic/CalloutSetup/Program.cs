using NATS.NKeys;

SetCurrentDirectoryToProjectRoot();

foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory()))
{
    Console.WriteLine(file);
}

var kp = KeyPair.CreatePair(PrefixByte.Account);

File.WriteAllText("server.conf", $$"""
                                 authorization {
                                   users: [ { user: auth } ]
                                   auth_callout {
                                     issuer: {{ kp.GetPublicKey() }}
                                     auth_users: [ auth ]
                                   }
                                 }
                                 """);

File.WriteAllText("account.nk", kp.GetSeed());

return;

static void SetCurrentDirectoryToProjectRoot()
{
    var d = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (!d.GetFiles(".project.root").Any())
    {
        d = d.Parent!;
    }
    Directory.SetCurrentDirectory(d.FullName);
}
using System.Diagnostics;

while (true)
{
    var processes = new List<Process>();
    foreach (var process in Process.GetProcesses())
    {
        if (process.ProcessName == "nats-server")
        {
            processes.Add(process);
        }
    }

    var next = Random.Shared.Next(0, processes.Count);
    var processToKill = processes[next];
    Console.WriteLine($"{DateTime.Now:O} Killing {processToKill.ProcessName} ({processToKill.Id})");
    processToKill.Kill();

    await Task.Delay(15_000);
}

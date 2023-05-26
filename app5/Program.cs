Console.WriteLine("Hi!");
// using System;
// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks;
// using NATS.Client.Core;
//
// Console.Error.WriteLine($"Starting..");
//
// Console.Error.WriteLine($"Connection ready");
//
// Console.Error.WriteLine($"Take baseline...");
// Console.ReadLine();
//
// await using var connection = new NatsConnection();
//
// var tasks = new List<ValueTask<TimeSpan>>();
// for (int i = 0; i < 100; i++)
// {
//     ValueTask<TimeSpan> task = connection.XxxPingAsync();
//     tasks.Add(task);
// }
//
// Console.Error.WriteLine($"Take 1...");
// Console.ReadLine();
//
// foreach (var valueTask in tasks)
// {
//     await valueTask;
// }
//
// Console.Error.WriteLine($"Take 2...");
// Console.ReadLine();
//
// return;
//
// Console.Error.WriteLine($"Sub1");
//
// await connection.XxxSubscribeAsync<byte[]>("foo", m=>
// {
//     Console.Error.Write("[CALLBACK-1] RCV: ");
//     foreach (var b in m)
//     {
//         Console.Error.Write($"{b:X} ");
//     }
//     Console.Error.WriteLine();
// });
//
// Console.Error.WriteLine($"Sub2");
//
// Console.Error.WriteLine("SUB2======================================================");
// await connection.XxxSubscribeAsync<byte[]>("foo", m =>
// {
//     Console.Error.Write("[CALLBACK-2] RCV: ");
//     foreach (var b in m)
//     {
//         Console.Error.Write($"{b:X} ");
//     }
//     Console.Error.WriteLine();
// }).ConfigureAwait(false);
//
// byte[] array = { 65, 66, 67 };
// ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>(array);
// NatsKey natsKey = new NatsKey("foo");
// for (int j = 0; j < 3; j++)
// {
//     Console.Error.WriteLine($"Pub{j}");
//
//     for (int i = 0; i < 10; i++)
//     {
//         Console.Error.WriteLine("PUB=============================================");
//         await connection.XxxPublishAsync(natsKey, readOnlyMemory);
//         Thread.Sleep(10);
//     }
//     
//     Console.Error.WriteLine($"Take{j}...");
//     Console.ReadLine();
// }
//
// Console.Error.WriteLine($"Detach...");
//
// Console.Error.WriteLine($"Bye");
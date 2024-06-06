while(1){ nats-server -c c1.conf; echo ===DEAD===; sleep 3 }
while(1){ nats-server -c c2.conf; echo ===DEAD===; sleep 3 }
while(1){ nats-server -c c3.conf; echo ===DEAD===; sleep 3 }

nats -s nats://127.0.0.1:4441 --user sys --password sys server report jetstream
cls; while(1){ tput cup 0 0; nats -s nats://127.0.0.1:4441 --user sys --password sys server report jetstream; sleep 2}
while(1){ cls; nats -s nats://127.0.0.1:4441 --user sys --password sys server report jetstream; sleep 5}

dotnet run
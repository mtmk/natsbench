#include "Socket.h"
#include <iostream>
#include <thread>
#include <memory>
#include <condition_variable>
#include <mutex>
#include <chrono>

using namespace std;

mutex mutex_;
condition_variable condVar;
int msgs = 1000000;

void reader_task(shared_ptr<SocketClient> s)
{
    int i = 0;
    while (1) {
        string l = s->ReceiveLine();
        if (l.empty()) break;

        if (l.rfind("PING", 0) == 0)
        {
            cout << l;
            cout.flush();
            s->SendLine("PONG");
            continue;
        }

        if (l.rfind("INFO", 0) == 0)
        {
            cout << l;
            cout.flush();
            condVar.notify_one();
            continue;
        }

        if (l.rfind("MSG", 0) == 0)
        {
            s->ReceiveLine();
            //cout << l;
            //cout.flush();
            if (++i == msgs)
            {
                break;
            }
            continue;
        }

        //cout << l;
        //cout.flush();
    }
}

int main() {

    try {
        auto s = make_shared<SocketClient>("127.0.0.1", 4222);

        thread reader_thread(reader_task, s);

        s->SendLine("CONNECT {\"verbose\":false}");

        unique_lock<std::mutex> lck(mutex_);
        condVar.wait(lck);

        s->SendLine("SUB foo 0");

        std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();

        for (int i = 0; i < msgs; i++)
        {
            s->SendBytes("PUB foo 128\r\n12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678\r\n");
            cout.flush();
        }
        cout << "pub done\n";
        reader_thread.join();
        cout << "sub done\n";

        std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::seconds>(end - begin).count() << "[s]" << std::endl;
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::milliseconds>(end - begin).count() << "[ms]" << std::endl;
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[µs]" << std::endl;
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::nanoseconds> (end - begin).count() << "[ns]" << std::endl;
    }
    catch (const char* s) {
        cerr << s << endl;
    }
    catch (std::string s) {
        cerr << s << endl;
    }
    catch (...) {
        cerr << "unhandled exception\n";
    }

    return 0;
}
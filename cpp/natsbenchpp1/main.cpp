#include "Socket.h"
#include <iostream>
#include <thread>
#include <memory>
#include <condition_variable>
#include <mutex>
#include <chrono>

//#define ASIO_WINDOWS_RUNTIME

//#include <asio.hpp>
//#include <boost/beast.hpp>

std::mutex mutex_;
std::condition_variable condVar;
int msgs = 10000000;

void reader_task(std::shared_ptr<SocketClient> s)
{
    int i = 0;
    while (1) {
        std::string l = s->ReceiveLine();
        if (l.empty()) break;

        if (l.rfind("PING", 0) == 0)
        {
            std::cout << l;
            std::cout.flush();
            s->SendLine("PONG");
            continue;
        }

        if (l.rfind("INFO", 0) == 0)
        {
            std::cout << l;
            std::cout.flush();
            condVar.notify_one();
            continue;
        }

        if (l.rfind("MSG", 0) == 0)
        {
            s->ReceiveLine();
            //cout << l;
            //cout.flush();
            //if (++i == 1000000)
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

    //asio::io_service io_service;
    //asio::ip::tcp::resolver resolver(io_service);
    //std::string host_("localhost");
    //resolver.async_resolve(
    //    asio::ip::tcp::resolver::query(host_, "4222"),
    //    [](asio::ip::tcp::resolver::iterator it)
    //    {
    //            //if (ec) {
    //            //    std::cout << "Error resolving " << host_ << ": "
    //            //        << ec.message();
    //            //    return;
    //            //}

    //            // For simplicity, we'll assume the first endpoint will always
    //            // be available.
    //            std::cout << ": resolved to " << it->endpoint() << std::endl;
    //            //do_connect(it->endpoint());
    //    });

    try {

        std::cout << "start" << std::endl;

        //std::string pub_msg = "PUB foo 128\r\n12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678\r\n";
        std::string pub_msg = "PUB foo 128\r\n12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678\r\n";
        int size1 = pub_msg.length();
        const char* pub_buf1 = pub_msg.c_str();
        int size = 0;
        int mul = 50;
        int total = mul * size1;
        char* pub_buf = new char[total];
        for (int i = 0; i < mul; i++)
        {
            for (auto c : pub_msg)
            {
                pub_buf[size] = c;
                size++;
            }
            //std::cout << i << std::endl;
        }
        int max = ((int)(msgs / mul));
        //int size = pub_msg.length();
        //const char* pub_buf = pub_msg.c_str();

        std::cout << "size:" << size << " MAX:" << max << std::endl;


        auto s = std::make_shared<SocketClient>("127.0.0.1", 4222);

        std::thread reader_thread(reader_task, s);

        s->SendLine("CONNECT {\"verbose\":false}");

        std::unique_lock<std::mutex> lck(mutex_);
        condVar.wait(lck);

        //s->SendLine("SUB foo 0");


        std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();

        for (int i = 0; i < max; i++)
        {
            s->SendChars((const char*)pub_buf, size);
            //std::cout.flush();
        }
        std::cout << "pub done\n";
        s->SendLine("SUB foo 0");
        s->SendBytes("PUB foo 1\r\n1\r\n");
        reader_thread.join();
        std::cout << "reader done\n";

        std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::seconds>(end - begin).count() << "[s]" << std::endl;
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::milliseconds>(end - begin).count() << "[ms]" << std::endl;
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[us]" << std::endl;
        std::cout << "Time difference = " << std::chrono::duration_cast<std::chrono::nanoseconds> (end - begin).count() << "[ns]" << std::endl;

        double x = (double)msgs / std::chrono::duration_cast<std::chrono::milliseconds>(end - begin).count();
        std::cout << "Time difference = " << (x/1000) << " msgs/ns" << std::endl;

    }
    catch (const char* s) {
        std::cerr << s << std::endl;
    }
    catch (std::string s) {
        std::cerr << s << std::endl;
    }
    catch (...) {
        std::cerr << "unhandled exception\n";
    }

    return 0;
}
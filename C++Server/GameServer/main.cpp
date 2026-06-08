#define _USE_MATH_DEFINES
#include <iostream>
#include <chrono>
#include <thread>
#include <cstdlib>
#include <ctime>
#include <direct.h>
#include <boost/asio.hpp>
#include "NetworkMessage.pb.h"
#include "Constants.h"
#include "LobbyManager.h"

using namespace std;
using namespace boost::asio;
using ip::udp;

int main() {
    std::srand(static_cast<unsigned>(std::time(nullptr)));
    std::ios::sync_with_stdio(false);
    std::cin.tie(nullptr);

    char cwd[1024];
    std::cout << "=== Game Server Started ===" << std::endl;
    std::cout << "Port: " << SERVER_PORT << std::endl;
    std::cout << "CWD: " << (_getcwd(cwd, sizeof(cwd)) ? cwd : "?") << std::endl;

    io_context context;
    udp::endpoint ep(ip::address_v4::any(), SERVER_PORT);
    udp::socket sock(context, ep);
    sock.non_blocking(true);

    LobbyManager lobby(sock);

    uint8_t buf[MAX_BUF_SIZE];
    boost::system::error_code ec;

    while (true) {
        while (true) {
            udp::endpoint sender;
            size_t len = 0;
            try {
                len = sock.receive_from(buffer(buf), sender, 0, ec);
            } catch (const std::exception&) {
                break;
            }

            if (ec) {
                if (ec != boost::asio::error::would_block) {
                    cerr << "Receive error: " << ec.message() << endl;
                }
                break;
            }

            if (len > 0) {
                lobby.ProcessMessage(buf, len, sender);
            }
        }

        lobby.Tick();

        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
}

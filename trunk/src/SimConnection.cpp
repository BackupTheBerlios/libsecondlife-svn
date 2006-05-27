#include "SimConnection.h"

SimConnection::SimConnection()
{
	_code = 0;
	_socket = NULL;
	_running = true;
	_buffer = (char*)malloc(SL_BUFFER_SIZE);
	_sequence = 1;
}

SimConnection::SimConnection(boost::asio::ipv4::address ip, unsigned short port, U32 code)
{
	_endpoint = boost::asio::ipv4::udp::endpoint(port, ip);
	_code = code;
	_socket = NULL;
	_running = true;
	_buffer = (char*)malloc(SL_BUFFER_SIZE);
	_sequence = 1;
}

SimConnection::~SimConnection()
{
#ifdef DEBUG
	std::cout << "SimConnection::~SimConnection() destructor called" << std::endl;
#endif
	delete _socket;
	free(_buffer);
}

bool SimConnection::operator==(SimConnection &p) {
	return (_endpoint == p.endpoint());
}

bool SimConnection::operator!=(SimConnection &p) {
	return !(*this == p);
}

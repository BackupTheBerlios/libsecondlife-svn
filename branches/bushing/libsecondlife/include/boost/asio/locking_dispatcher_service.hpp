//
// locking_dispatcher_service.hpp
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//
// Copyright (c) 2003-2005 Christopher M. Kohlhoff (chris at kohlhoff dot com)
//
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt)
//

#ifndef BOOST_ASIO_LOCKING_DISPATCHER_SERVICE_HPP
#define BOOST_ASIO_LOCKING_DISPATCHER_SERVICE_HPP

#if defined(_MSC_VER) && (_MSC_VER >= 1200)
# pragma once
#endif // defined(_MSC_VER) && (_MSC_VER >= 1200)

#include <boost/asio/detail/push_options.hpp>

#include <boost/asio/detail/push_options.hpp>
#include <memory>
#include <boost/asio/detail/pop_options.hpp>

#include <boost/asio/basic_demuxer.hpp>
#include <boost/asio/demuxer_service.hpp>
#include <boost/asio/detail/locking_dispatcher_service.hpp>
#include <boost/asio/detail/noncopyable.hpp>

namespace boost {
namespace asio {

/// Default service implementation for a locking dispatcher.
template <typename Allocator = std::allocator<void> >
class locking_dispatcher_service
  : private noncopyable
{
public:
  /// The demuxer type for this service.
  typedef basic_demuxer<demuxer_service<Allocator> > demuxer_type;

private:
  // The type of the platform-specific implementation.
  typedef detail::locking_dispatcher_service<demuxer_type> service_impl_type;

public:
  /// The type of the locking dispatcher.
#if defined(GENERATING_DOCUMENTATION)
  typedef implementation_defined impl_type;
#else
  typedef typename service_impl_type::impl_type impl_type;
#endif

  /// Constructor.
  explicit locking_dispatcher_service(demuxer_type& demuxer)
    : service_impl_(demuxer.get_service(service_factory<service_impl_type>()))
  {
  }

  /// Get the demuxer associated with the service.
  demuxer_type& demuxer()
  {
    return service_impl_.demuxer();
  }

  /// Return a null locking dispatcher implementation.
  impl_type null() const
  {
    return service_impl_.null();
  }

  /// Create a new locking dispatcher implementation.
  void create(impl_type& impl)
  {
    service_impl_.create(impl);
  }

  /// Destroy a locking dispatcher implementation.
  void destroy(impl_type& impl)
  {
    service_impl_.destroy(impl);
  }

  /// Request a dispatcher to invoke the given handler.
  template <typename Handler>
  void dispatch(impl_type& impl, Handler handler)
  {
    service_impl_.dispatch(impl, handler);
  }

  /// Request a dispatcher to invoke the given handler and return immediately.
  template <typename Handler>
  void post(impl_type& impl, Handler handler)
  {
    service_impl_.post(impl, handler);
  }

private:
  // The service that provides the platform-specific implementation.
  service_impl_type& service_impl_;
};

} // namespace asio
} // namespace boost

#include <boost/asio/detail/pop_options.hpp>

#endif // BOOST_ASIO_LOCKING_DISPATCHER_SERVICE_HPP
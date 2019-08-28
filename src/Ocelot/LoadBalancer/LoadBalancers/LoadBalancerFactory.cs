using Ocelot.Configuration;
using Ocelot.Infrastructure;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery;
using System.Threading.Tasks;

namespace Ocelot.LoadBalancer.LoadBalancers
{
    public class LoadBalancerFactory : ILoadBalancerFactory
    {
        private readonly IServiceDiscoveryProviderFactory _serviceProviderFactory;

        public LoadBalancerFactory(IServiceDiscoveryProviderFactory serviceProviderFactory)
        {
            _serviceProviderFactory = serviceProviderFactory;
        }

        public Task<Response<ILoadBalancer>> Get(DownstreamReRoute reRoute, ServiceProviderConfiguration config)
        {
            var response = _serviceProviderFactory.Get(config, reRoute);

            if (response.IsError)
            {
                return Task.FromResult<Response<ILoadBalancer>>(new ErrorResponse<ILoadBalancer>(response.Errors));
            }

            var serviceProvider = response.Data;

            switch (reRoute.LoadBalancerOptions?.Type)
            {
                case nameof(RoundRobin):
                    return Task.FromResult<Response<ILoadBalancer>>(new OkResponse<ILoadBalancer>(new RoundRobin(async () => await serviceProvider.Get())));

                case nameof(LeastConnection):
                    return Task.FromResult<Response<ILoadBalancer>>(new OkResponse<ILoadBalancer>(new LeastConnection(async () => await serviceProvider.Get(), reRoute.ServiceName)));

                case nameof(CookieStickySessions):
                    var loadBalancer = new RoundRobin(async () => await serviceProvider.Get());
                    var bus = new InMemoryBus<StickySession>();
                    return Task.FromResult<Response<ILoadBalancer>>(new OkResponse<ILoadBalancer>(new CookieStickySessions(loadBalancer, reRoute.LoadBalancerOptions.Key, reRoute.LoadBalancerOptions.ExpiryInMs, bus)));

                default:
                    return Task.FromResult<Response<ILoadBalancer>>(new OkResponse<ILoadBalancer>(new NoLoadBalancer(async () => await serviceProvider.Get())));
            }
        }
    }
}

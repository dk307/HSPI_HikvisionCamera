using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace Hspi.Camera.Onvif.Behaviour
{
    class CustomEndpointBehavior : IEndpointBehavior
    {
        public CustomEndpointBehavior(IClientMessageInspector clientInspector)
        {
            this.clientInspector = clientInspector ?? throw new ArgumentNullException(nameof(clientInspector));
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            // Method intentionally left empty.
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(clientInspector);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            // Method intentionally left empty.
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            // Method intentionally left empty.
        }

        private readonly IClientMessageInspector clientInspector;
    }
}

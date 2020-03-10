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
            if (clientInspector == null)
                throw new ArgumentNullException(nameof(clientInspector));

            this.clientInspector = clientInspector;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(clientInspector);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }

        private readonly IClientMessageInspector clientInspector;
    }
}

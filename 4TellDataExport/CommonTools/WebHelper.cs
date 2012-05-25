//using System;
//using System.Collections.Generic;
//using System.Runtime.Serialization;
//using System.Text;
//using System.Diagnostics;
//using System.IO;
//using System.Configuration;
//using System.Timers;
//using System.Threading;
using System.ServiceModel; //OperationContext
using System.ServiceModel.Channels; //MessageProperties RemoteEndpointMessageProperty
using System.ServiceModel.Web; //WebOperationContext


namespace _4_Tell.Utilities
{

	public class WebHelper
	{
		public void GetContextOfRequest(out string ip, out string method, out string parameters)
		{
			ip = method = parameters = "";

			OperationContext context = OperationContext.Current;
			if (context == null) return;

			MessageProperties messageProperties = context.IncomingMessageProperties;
			if (messageProperties != null)
			{
				if (messageProperties.Via != null)
				{
					parameters = messageProperties.Via.Query;
					method = messageProperties.Via.LocalPath;
				}
			}

			WebOperationContext webContext = WebOperationContext.Current;
			if ((webContext != null) && (webContext.IncomingRequest != null)
				&& (webContext.IncomingRequest.Headers["X-Forwarded-For"] != null)) //forwarded IP through load balancer
				ip = webContext.IncomingRequest.Headers["X-Forwarded-For"];
			else if (messageProperties != null)
			{
				RemoteEndpointMessageProperty endpointProperty = messageProperties[RemoteEndpointMessageProperty.Name]
						as RemoteEndpointMessageProperty;
				if (endpointProperty != null)
					ip = endpointProperty.Address;
			}
		}

		public WebContextProxy GetContextOfRequest()
		{
			OperationContext context = OperationContext.Current;
			if (context == null) return null ;

			WebContextProxy wc = new WebContextProxy();

			MessageProperties messageProperties = context.IncomingMessageProperties;
			if (messageProperties != null)
			{
				if (messageProperties.Via != null)
				{
					wc.parameters = messageProperties.Via.Query;
					wc.method = messageProperties.Via.LocalPath;
				}
			}

			WebOperationContext webContext = WebOperationContext.Current;
			if ((webContext != null) && (webContext.IncomingRequest != null)
				&& (webContext.IncomingRequest.Headers["X-Forwarded-For"] != null)) //forwarded IP through load balancer
				wc.ip = webContext.IncomingRequest.Headers["X-Forwarded-For"];
			else if (messageProperties != null)
			{
				RemoteEndpointMessageProperty endpointProperty = messageProperties[RemoteEndpointMessageProperty.Name]
						as RemoteEndpointMessageProperty;
				if (endpointProperty != null)
					wc.ip = endpointProperty.Address;
			}
			return wc;
		}
	}
}

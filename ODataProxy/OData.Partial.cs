using Microsoft.OData.Client;
using System;
using System.Collections.Generic;
using System.Text;
using WorkZone.OData4.Client;
using WorkZone.OData4.Client.Extensions;


namespace WorkZone
{
    public partial class OData : IPropertyTrackingContext, IODataRequestHandler
    {
        public PropertyTracker PropertyTracker { get; private set; }
        public ODataRequestFeatures ODataRequestFeatures { get; private set; }

        static AzureAuthentication azureAuthentication = new AzureAuthentication(
                tenantId: "", // TODO: Add your tenant ID here
                clientId: "", // TODO: Add your client ID here
                clientSecret: "", // TODO: Add your client secret here
                workzoneApplicationIdUri: "" // TODO: Add your WorkZone application ID URI here
            );


        public OData() : this(new Uri("")) // TODO: Add your OData service URI here
        {
        }

        partial void OnContextCreated()
        {
            ODataRequestFeatures = new ODataRequestFeatures(this);
            PropertyTracker = new PropertyTracker(this)
            {
                EnableNullPatching = true
            };

            EntityParameterSendOption = EntityParameterSendOption.SendOnlySetProperties;
            AutoNullPropagation = true;
            ODataRequestFeatures.UseLogApplication = "WorkZone.Uffe.Demo";
            ODataRequestFeatures.OmitNullValues = true;
            ODataRequestFeatures.GetAccessToken = azureAuthentication.GetAccessToken;
        }
    }
}

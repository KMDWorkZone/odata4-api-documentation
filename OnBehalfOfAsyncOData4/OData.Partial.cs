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
                tenantId: "1b04dcf1-718d-4d5f-b1b6-07d8437ffa5f",
                clientId: "d9107597-0f22-42c7-a2df-7375fd5ffd99",
                clientSecret: "uoE8Q~umFPTNq~1SNfWNH~wR8VmxGZkZR5uPwc3i",
                workzoneApplicationIdUri: "https://freepdb1.pqmdocker.onmicrosoft.com"
            );


        public OData(string? onBehalfOfUser = null) : this(new Uri("https://freepdb1.pqmdocker.onmicrosoft.com/OData4/"))
        {
            //ODataRequestFeatures.OnBehalfOfUser = onBehalfOfUser;   Use with OnBehalfOf and integration user 
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

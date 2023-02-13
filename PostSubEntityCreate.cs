using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Assessment.Plugins
{
    public class PostSubEntityCreate : PluginBase
    {
        public PostSubEntityCreate(string unsecure, string secure) : base(typeof(PostSubEntityCreate))
        {
        }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localContext)
        {
            if (localContext == null) throw new InvalidPluginExecutionException(nameof(localContext));
            var tracingService = localContext.TracingService;

            try
            {
                var context = (IPluginExecutionContext)localContext.PluginExecutionContext;
                var currentUserService = localContext.CurrentUserService;

                var subEntity = (Entity)context.InputParameters["Target"];

                if (subEntity.LogicalName == "new_subentity" && context.MessageName == "Create" && subEntity.Attributes.Contains("new_master"))
                {
                    var relatedProperties = GetRelatedProperties(currentUserService, ((EntityReference)subEntity["new_master"]).Id);

                    foreach(Entity property in relatedProperties.Entities)
                    {
                        property["new_subid"] = new EntityReference(subEntity.LogicalName, subEntity.Id);

                        currentUserService.Update(property);
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService?.Trace("Error on Plugin Assessment.Plugins.PostSubEntityCreate : " + ex.ToString());
                throw new InvalidPluginExecutionException("Error on Plugin Assessment.Plugins.PostSubEntityCreate.", ex);
            }
        }

        private EntityCollection GetRelatedProperties(IOrganizationService organizationService, Guid masterPropertyId)
        {
            var qryProperties = new QueryExpression()
            {
                EntityName = "new_property",
                ColumnSet = new ColumnSet(""),
                NoLock = true, 
                Criteria =
                {
                    Conditions = {
                        new ConditionExpression("new_masterid", ConditionOperator.Equal,masterPropertyId),
                        new ConditionExpression("new_subid", ConditionOperator.Null)
                    },
                },
            };

            return organizationService.RetrieveMultiple(qryProperties);
        }
    }
}

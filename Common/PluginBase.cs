﻿
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.ServiceModel;

namespace Assessment.Plugins
{
    /// <summary>
    /// Base class for all plug-in classes.
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>    
    public abstract class PluginBase : IPlugin
    {
        /// <summary>
        /// Gets or sets the name of the plugin class.
        /// </summary>
        /// <value>The name of the child class.</value>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "PluginBase")]
        protected string PluginClassName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginBase"/> class.
        /// </summary>
        /// <param name="pluginClassName">The <see cref=" cred="Type"/> of the derived class.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "PluginBase")]
        internal PluginBase(Type pluginClassName)
        {
            PluginClassName = pluginClassName.ToString();
        }

        /// <summary>
        /// Main entry point for he business logic that the plug-in is to execute.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <remarks>
        /// For improved performance, Microsoft Dynamics 365 caches plug-in instances. 
        /// The plug-in's Execute method should be written to be stateless as the constructor 
        /// is not called for every invocation of the plug-in. Also, multiple system threads 
        /// could execute the plug-in at the same time. All per invocation state information 
        /// is stored in the context. This means that you should not use global variables in plug-ins.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Execute")]
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException("serviceProvider");
            }

            // Construct the local plug-in context.
            var localPluginContext = new LocalPluginContext(serviceProvider);

            localPluginContext.Trace($"Entered {PluginClassName}.Execute() " +
                 $"Correlation Id: {localPluginContext.PluginExecutionContext.CorrelationId}, " +
                 $"Initiating User: {localPluginContext.PluginExecutionContext.InitiatingUserId}");

            try
            {
                // Invoke the custom implementation 
                ExecuteCdsPlugin(localPluginContext);

                // now exit - if the derived plug-in has incorrectly registered overlapping event registrations,
                // guard against multiple executions.
                return;
            }
            catch (FaultException<OrganizationServiceFault> orgServiceFault)
            {
                localPluginContext.Trace($"Exception: {orgServiceFault.ToString()}");

                // Handle the exception.
                throw new InvalidPluginExecutionException($"OrganizationServiceFault: {orgServiceFault.Message}", orgServiceFault);
            }
            finally
            {
                localPluginContext.Trace($"Exiting {PluginClassName}.Execute()");
            }
        }


        /// <summary>
        /// Placeholder for a custom plug-in implementation. 
        /// </summary>
        /// <param name="localPluginContext">Context for the current plug-in.</param>
        protected virtual void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            // Do nothing. 
        }

    }

    //This interface provides an abstraction on top of IServiceProvider for commonly used PowerApps CDS Plugin development constructs
    public interface ILocalPluginContext
    {
        // The PowerApps CDS organization service for current user account
        IOrganizationService CurrentUserService { get; }

        // The PowerApps CDS organization service for system user account
        IOrganizationService SystemUserService { get; }

        // IPluginExecutionContext contains information that describes the run-time environment in which the plugin executes, information related to the execution pipeline, and entity business information
        IPluginExecutionContext PluginExecutionContext { get; }

        // Synchronous registered plugins can post the execution context to the Microsoft Azure Service Bus.
        // It is through this notification service that synchronous plug-ins can send brokered messages to the Microsoft Azure Service Bus
        IServiceEndpointNotificationService NotificationService { get; }

        // Provides logging run time trace information for plug-ins. 
        ITracingService TracingService { get; }

        // Writes a trace message to the CDS trace log
        void Trace(string message);
        void SetOutputParameters(ILocalPluginContext localPluginContext, bool success, string message);
    }

    /// <summary>
    /// Plug-in context object. 
    /// </summary>
    public class LocalPluginContext : ILocalPluginContext
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "LocalPluginContext")]
        internal IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// The PowerApps CDS organization service for current user account.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "LocalPluginContext")]
        public IOrganizationService CurrentUserService { get; private set; }

        /// <summary>
        /// The PowerApps CDS organization service for system user account.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "LocalPluginContext")]
        public IOrganizationService SystemUserService { get; private set; }

        /// <summary>
        /// IPluginExecutionContext contains information that describes the run-time environment in which the plug-in executes, information related to the execution pipeline, and entity business information.
        /// </summary>
        public IPluginExecutionContext PluginExecutionContext { get; private set; }

        /// <summary>
        /// Synchronous registered plug-ins can post the execution context to the Microsoft Azure Service Bus. <br/> 
        /// It is through this notification service that synchronous plug-ins can send brokered messages to the Microsoft Azure Service Bus.
        /// </summary>
        public IServiceEndpointNotificationService NotificationService { get; private set; }


        /// <summary>
        /// Provides logging run-time trace information for plug-ins. 
        /// </summary>
        public ITracingService TracingService { get; private set; }

        /// <summary>
        /// Helper object that stores the services available in this plug-in.
        /// </summary>
        /// <param name="serviceProvider"></param>
        public LocalPluginContext(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException("serviceProvider");
            }

            // Obtain the execution context service from the service provider.
            PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Obtain the tracing service from the service provider.
            TracingService = new LocalTracingService(serviceProvider);

            // Get the notification service from the service provider.
            NotificationService = (IServiceEndpointNotificationService)serviceProvider.GetService(typeof(IServiceEndpointNotificationService));

            // Obtain the organization factory service from the service provider.
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            // Use the factory to generate the organization service.
            CurrentUserService = factory.CreateOrganizationService(PluginExecutionContext.UserId);

            // Use the factory to generate the organization service.
            SystemUserService = factory.CreateOrganizationService(null);

        }

        /// <summary>
        /// Writes a trace message to the CRM trace log.
        /// </summary>
        /// <param name="message">Message name to trace.</param>
        public void Trace(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || TracingService == null)
            {
                return;
            }

            if (PluginExecutionContext == null)
            {
                TracingService.Trace(message);
            }
            else
            {
                TracingService.Trace(
                    "{0}, Correlation Id: {1}, Initiating User: {2}",
                    message,
                    PluginExecutionContext.CorrelationId,
                    PluginExecutionContext.InitiatingUserId);
            }
        }
        public void SetOutputParameters(ILocalPluginContext localPluginContext, bool success, string message)
        {
            localPluginContext.PluginExecutionContext.OutputParameters["Success"] = success;
            localPluginContext.PluginExecutionContext.OutputParameters["Message"] = message;
        }
    }

    // Specialized ITracingService implementation that prefixes all traced messages with a time delta for Plugin performance diagnostics
    public class LocalTracingService : ITracingService
    {
        private readonly ITracingService _tracingService;

        private DateTime _previousTraceTime;

        public LocalTracingService(IServiceProvider serviceProvider)
        {
            DateTime utcNow = DateTime.UtcNow;

            var context = (IExecutionContext)serviceProvider.GetService(typeof(IExecutionContext));

            DateTime initialTimestamp = context.OperationCreatedOn;

            if (initialTimestamp > utcNow)
            {
                initialTimestamp = utcNow;
            }

            _tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            _previousTraceTime = initialTimestamp;
        }

        public void Trace(string message, params object[] args)
        {
            var utcNow = DateTime.UtcNow;

            // The duration since the last trace.
            var deltaMilliseconds = utcNow.Subtract(_previousTraceTime).TotalMilliseconds;

            _tracingService.Trace($"[+{deltaMilliseconds:N0}ms)] - {message}");

            _previousTraceTime = utcNow;
        }
    }
}
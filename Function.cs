using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Trace;
using System.Diagnostics;
using Amazon.Lambda.Core;
using System.Net.Http;
using System.Net.Http.Headers;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorldLambda
{
    //Handler: HelloWorldLambda::HelloWorldLambda.FunctionAuto::TracingFunctionHandler
    public class FunctionAuto
    {
        public static TracerProvider tracerProvider;

        static FunctionAuto()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAWSInstrumentation() //Add auto-instrumentation for AWS SDK
                .AddHttpClientInstrumentation()
                .AddAWSLambdaConfigurations()
                .AddOtlpExporter()
                .Build();
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string FunctionHandler(string input, ILambdaContext context)
        {
            return input?.ToUpper();   
        }

        public string TracingFunctionHandler(string input, ILambdaContext context)
        {
            return AWSLambdaWrapper.Trace(tracerProvider, FunctionHandler, input, context);
        }
    }

    //Handler: HelloWorldLambda::HelloWorldLambda.FunctionManual::FunctionHandler
    public class FunctionManual
    {

        private static readonly HttpClient client = new HttpClient();
        public static TracerProvider tracerProvider;
        //Defines the OpenTelemetry resource attribute "service.name" which is mandatory
        private const string servicename = "AWS Lambda";

        //Defines the OpenTelemetry Instrumentation Library.
        private const string activitySource = "Honeycomb";

        //Provides the API for starting/stopping activities.
        private static readonly ActivitySource MyActivitySource = new ActivitySource(activitySource);
        static FunctionManual()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySource)
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(servicename))
                .AddAWSInstrumentation() //Add auto-instrumentation for AWS SDK
                .AddHttpClientInstrumentation()
                .AddOtlpExporter()
                .Build();
        }

        private static async Task ProcessRepositories()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            var stringTask = client.GetStringAsync("https://api.github.com/orgs/dotnet/repos");

            var msg = await stringTask;
            Console.Write(msg);
        }
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(string input, ILambdaContext context)
        {
            using(tracerProvider) {
                using (var activity = MyActivitySource.StartActivity("Invoke"))
                {
                    await ProcessRepositories();

                    activity?.SetTag("input", input);
                    return input?.ToUpper();
                }
            }
        }
    }
}

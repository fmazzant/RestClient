# RestClient

RestClient library provides a simple connector to consume REST services.

To use it in your project, add the Mafe.RestClient NuGet package to your project.

[![Nuget](https://img.shields.io/nuget/v/Mafe.RestClient?style=flat-square)](https://www.nuget.org/packages/Mafe.RestClient)

Supported target frameworks: `net10.0`, `net8.0`, `netstandard2.1`, `netstandard2.0` (.NET Framework 4.6.2+, Xamarin, Unity, ...).

Highlights:

* fluent and immutable builder, safe to reuse and to share between threads
* pooled connections, retry policy, refresh token, timeout, cancellation
* JSON (System.Text.Json, Native AOT ready), XML or custom serialization, `multipart/form-data`, form-url-encoded
* real streaming for downloads, upload/download progress
* `ILogger` logging and OpenTelemetry tracing
* `IHttpClientFactory` integration with the [Mafe.RestClient.Extensions.DependencyInjection](#dependency-injection) package

> Upgrading from 1.x? Read [Migrating to 2.0](#migrating-to-20): version 2.0.0 contains breaking changes.
> Install it with `dotnet add package Mafe.RestClient`. See the [CHANGELOG](CHANGELOG.md).

### What is RestClient?

The goal of RestClient is to enable developers to easily connect to any server in a really easy way. 
Just define your Data Transfer Object (Dto) and start playing with the client!

Use Build() method to create a RestBuilder from Rest:

```c#
var rest = Rest.Build();
```

To create a simple get call, just do like this:

```c#
var rest = Rest.Build();
var result = rest.Url("[URL]").Get();
```
or we can use GetAsync() method:

```c#
var rest = Rest.Build();
var result = await rest.Url("[URL]").GetAsync();
```

Whenever you find the word "[URL]" in this document it reffers to the base URL definition of the webAPI.

We can define a Root() endpoint and use it to build the request.

```c#
public RestBuilder Root() => rest.Url("https://mywebapi.mydomain.com");
```

and then, we can use it, like this:

```c#
public RestBuilder Root() => Rest.Build().Url("https://mywebapi.mydomain.com");
var result = Root()
    .Command("/my-action")
    .Payload("mystring")
    .Post();
```

### RestProperties

To use RestProperties to configure yuor rest root point. To create a simple configuration, just like this:

```c#
RestProperties properties = new RestProperties
{
    EndPoint = new Uri("[URL]"), //if you use .Url("[URL]") you override it
    BufferSize = 4096,
    CertificateOption = ClientCertificateOption.Manual,
    Timeout = TimeSpan.FromMinutes(2)
};
```

Use Build() method with properties to create a RestBuilder from Rest:

```c#
var rest = Rest.Build(properties);
```

The properties are copied: changing `properties` after `Build` does not affect the builder.
`RestBuilder.Properties` returns a copy too: use the builder methods (`Url`, `Timeout`, `BufferSize`) to change them.

### Immutable builder

Every method of `RestBuilder` returns a new, independent instance. A configured builder can be safely stored and reused
as the base of many requests (also from different threads):

```c#
var users = Rest.Build().Url("[URL]").Command("/users");

var user1 = await users.Command(1).GetAsync<User>();   // [URL]/users/1
var user2 = await users.Command(2).GetAsync<User>();   // [URL]/users/2
var all   = await users.GetAsync<List<User>>();        // [URL]/users
```

### Connections

RestClient keeps a pool of HTTP handlers, one for each combination of compression, credentials, certificate option and
certificate validation callback. The TCP/TLS connections are reused by all the requests (no socket exhaustion, no handshake
for every call) and, on .NET 8+, they are recycled every 2 minutes so that DNS changes are honored. Cookies are not stored.

### Using an existing HttpClient (IHttpClientFactory)

You can send the requests with your own `HttpClient`, for example one created by `IHttpClientFactory`
(see also [Dependency Injection](#dependency-injection)):

```c#
HttpClient httpClient = httpClientFactory.CreateClient("api");

var result = await Rest.Build(httpClient)       // or Rest.Build().UseHttpClient(httpClient)
    .Url("[URL]")
    .Command("/users")
    .GetAsync<List<User>>();
```

The external `HttpClient` is never disposed by RestClient. `NetworkCredential`, `CertificateValidation`,
`RestProperties.CertificateOption`, `EnableGZipCompression` and `OnConfigureHttpClient` configure the `HttpClient` managed
by RestClient, so they are not applied to an external one: configure them on your handler.
Headers, authentication, timeout, retry, serialization, progress and all the events work in both cases.

### Dependency Injection

The `Mafe.RestClient.Extensions.DependencyInjection` package registers the builders on top of `IHttpClientFactory`:

```c#
// named client
services.AddRestClient("github", builder => builder
        .Url("https://api.github.com")
        .Header("User-Agent", "my-app"))
    .AddStandardResilienceHandler();   // any IHttpClientBuilder extension: handlers, resilience, ...

public class ReposService(IRestClientFactory factory)
{
    private readonly RestBuilder github = factory.CreateClient("github");

    public Task<RestResult<Repo>> GetAsync(string owner, string name)
        => github.Command("repos").Command(owner).Command(name).GetAsync<Repo>();
}

// typed client: UsersClient receives a configured RestBuilder in its constructor
services.AddRestClient<UsersClient>((provider, builder) => builder
    .Url(provider.GetRequiredService<IConfiguration>()["Api:Url"]!));
```

The builders created by `IRestClientFactory` log with the category `Mafe.RestClient.{name}`.

### Certificate Validation

Validation of X.509 certificates is essential to create secure SSL/TLS sessions not vulnerable to man-in-the-middle attacks.

The certificate chain validation includes these steps:

* The certificate is issued by its parent Certificate Authority or the root CA trusted by the system.
* Each CA is allowed to issue certificates.
* Each certificate in the chain is not expired.

It's not recommended to reinvent the wheel by implementing custom certificate chain validation.

TLS libraries provide built-in certificate validation functions that should be used.

```c#
List<string> validCerts = new List<string>() {
    "CERT STRING"
};

var result = Rest.Build()
    .CertificateValidation((sender, certificate, chain, errors) =>
    {
      // for development, trust all certificates
      if (development) return true; 
      
      // Compliant: trust only some certificates
      return errors == SslPolicyErrors.None 
              && validCerts.Contains(certificate.GetCertHashString()); 
    })
    .Url("[URL]")
    .Get();
```

By default RestClient uses the validation of the operating system. The callback is applied only to the requests of the builder.
Since the HTTP handlers are pooled by configuration, define the callback once (a static method or a field) instead of
creating a new closure for every request.

### Authentication

As defined by HTTP/1.1 [RFC2617], the application should send the access_token directly in the Authorization request header.
You can do so by including the bearer token's access_token value in the HTTP request body as 'Authorization: Bearer {access_token_value}'.

If an authenticated user has a bearer token's access_token or refresh_token that is expired, then a '401 - Unauthorized (invalid or expired refresh token)' error is returned.

```c#
var result = Rest.Build()
    .Authentication(() => new AuthenticationHeaderValue("Bearer", "[Token]"))
    .Url("[URL]")
    .Get();
```

### Refresh Token

A valid bearer token (with active access_token or refresh_token properties) keeps the user's authentication alive without requiring him or her to re-enter their credentials frequently.
The access_token can be used for as long as it’s active, which is up to one hour after login or renewal. The refresh_token is active for 336 hours (14 days). After the access_token expires, an active refresh_token can be used to get a new access_token / refresh_token pair as shown in the following example. This cycle can continue for up to 90 days after which the user must log in again. If the refresh_token expires, the tokens cannot be renewed and the user must log in again.

To refresh a token, use "RefreshTokenInvoke" automatically.

```c#
var url = "[URL]";
var result = Rest.Build()
    .Authentication(() => new AuthenticationHeaderValue("Bearer", "[Token]"))
    .RefreshToken(true)
    .RefreshTokenInvoke(async () =>
    {
        var result = await rest
            .Url(url)
            .Command("/refresh")
            .GetAsync<TokenObjectResponse>();   
        doSomethings(); //store the token inside your env.
        return result;
    })
    .Command("/detail")
    .Url(url)
    .Get();
```

When the response is `401 Unauthorized` the refresh func is invoked and, if it returns a 2xx result, the request is sent again
(once). The `Authentication` func is evaluated for every attempt, so the new token is used.
When many concurrent requests of the same builder receive 401, the refresh func is invoked **only once** and the other
requests wait for it. The requests sent from inside the refresh func never trigger another refresh.

A refresh_token should be revoked:
* If a user is no longer permitted to make requests on the API, or
* If the access_token or refresh_token have been compromised.

### Network Credential

The NetworkCredential class is a base class that supplies credentials in password-based authentication schemes such as basic, digest, NTLM, and Kerberos. 
Classes that implement the ICredentials interface, such as the CredentialCache class, return NetworkCredential objects.
This class does not support public key-based authentication methods such as Secure Sockets Layer (SSL) client authentication.

```c#
var result = Rest.Build()
    .NetworkCredential("myUsername","myPassword")
    .Url("[URL]")
    .Get();
```

```c#
var result = rest
    .NetworkCredential(() =>  new System.Net.NetworkCredential("myUsername","myPassword"))
    .Url("[URL]")
    .Get();
```

### Headers

The Headers collection contains the protocol headers associated with the request. The Header((h)=>{}) method allows you to add the list of keys.

```c#
var result = Rest.Build()
    .Header((h) => {
        if(!h.Contains("auth-custom"))
            h.Add("auth-custom", "value");
    })
    .Header("X-Correlation-Id", Guid.NewGuid().ToString())
    .Url("[URL]")
    .Get();
```

Headers are added to the request message (not to `HttpClient.DefaultRequestHeaders`) and calling `Header` more than once
adds all the headers.

### Serialization

Two types of serialization are supported by RestClient: Xml and Json, but it is possible to implementate ISerializerContent to customize the serialization.
RestClient uses .Json() to serialize an object into json. Json is the default serialization and it is based on System.Text.Json.

When no options are provided `JSON.DefaultOptions` is used: it is `new JsonSerializerOptions(JsonSerializerDefaults.Web)`,
so property names are written in camelCase and read case-insensitively.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Json()
    .Get<ResponseObject>();
```

It is possible to pass an json serializer options to .Json() method, like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Json(new JsonSerializerOptions {
        WriteIndented = true
    })
    .Get<ResponseObject>();
```

Successful JSON responses are deserialized directly from the network stream, without buffering the body as a string
(unless `OnPreviewContentResponseAsString` is used). An empty body (for example `204 No Content`) is a `null` content.

#### Native AOT and trimming

The JSON serializer is compatible with Native AOT and trimming when it uses a source generated `JsonSerializerContext`:

```c#
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<User>))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
public partial class AppJsonContext : JsonSerializerContext { }

var users = await Rest.Build()
    .Url("[URL]")
    .Json(AppJsonContext.Default)
    .GetAsync<List<User>>();
```

`Xml()` uses `XmlSerializer`, which is not compatible with trimming: the method is annotated, so the compiler warns you.

RestClient uses .Xml() to serialize an object into xml.

```c#
var result = rest
    .Url("[URL]")
    .Xml()
    .Get<ResponseObject>();
```

It is possible to pass the settings to .Xml() method: the reader settings are used to read the response, the writer settings to write the payload.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Xml(new XmlReaderSettings { IgnoreWhitespace = true }, new XmlWriterSettings { Indent = true })
    .Get<ResponseObject>();
```

### Custom Serialization

Below an example on how you can do a custom Serialization by implementing the ISerializerContent interface:

```c#
public class MyCustomSerializer : ISerializerContent
{
    public string MediaTypeAsString => throw new NotImplementedException();

    public object? DeserializeObject(string value, Type typeOf, object? options = null)
    {
        throw new NotImplementedException();
    }

    public string SerializeObject(object? value, Type typeOf, object? options = null)
    {
        throw new NotImplementedException();
    }
}
```

Implement `IAsyncSerializerContent` (it adds `DeserializeObjectAsync(Stream, ...)`) to deserialize directly from the response stream.

Now, we can use MyCustomSerializer like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .CustomSerializer(new MyCustomSerializer { }, myOptions) // options are optional
    .Get<ResponseObject>();
```

### BufferSize

BufferSize can be use to set the buffer size during upload and download stream. Default value is 80Kb

```c#
var result = Rest.Build()
    .Url("[URL]")
    .BufferSize(4096 * 5 * 5) //100Kb
    .Get();
```

### GZip Compression

Enables gzip compression during communication with a specified resource:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .EnableGZipCompression()
    .Get();
```

The library sends the `Accept-Encoding` header and uncompresses automatically the response (gzip, deflate and, on .NET 8+, brotli).

### Response types

Every HTTP method has a non generic version, that returns the body as `string`, and a generic one. `T` can be:

| T | Content |
|---|---|
| `string` | the raw body |
| `byte[]` | the raw body |
| `Stream` | the body **not buffered**: it is read from the network while you read it. Dispose the result when done. |
| any other type | the body deserialized with the configured serializer (JSON by default) |

```c#
RestResult<User> user = await root.Command("users/1").GetAsync<User>();
RestResult<byte[]> image = await root.Command("avatar.png").GetAsync<byte[]>();

using RestResult<Stream> file = await root.Command("big-file.zip").GetAsync<Stream>();
await file.Content!.CopyToAsync(destination);
```

The available methods are `Get`, `Post`, `Put`, `Patch`, `Delete`, `Head` and `Options` (each one with an `Async` version).
Any other method can be sent with `SendAsync<T>(HttpMethod)` / `Send<T>(HttpMethod)`:

```c#
var result = await root.Command("calendar").SendAsync<string>(new HttpMethod("REPORT"));
```

### Get

GET is one of the most common HTTP methods and GET is used to request data from a specified resource 

```c#
var rest = Rest.Build();

var result = rest
    .Url("[URL]")
    .Get();

var result = await rest
    .Url("[URL]")
    .GetAsync();
```

Some other notes on GET requests:

* GET requests can be cached
* GET requests remain in the browser history
* GET requests can be bookmarked
* GET requests should never be used when dealing with sensitive data
* GET requests have length restrictions
* GET requests are only used to request data (not modify)

Note that the query string (name/value pairs) is sent in the URL of a GET request.

### Parameters as query string 

In some cases we need to use arguments as query string. We can use the method Parameter(key, value) to resolve it like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/path")
    .Parameter("id","10")
    .Parameter("type", "myType")
    .Get();
```

The url generated is: [URL]/path?id=10&type=myType

Keys and values are url-encoded (`"a b&c"` becomes `a%20b%26c`) and formatted with the invariant culture
(`1.5` is always `1.5`, `true` is `true`, dates are ISO 8601). Do not encode the values yourself.

Setting a key again replaces its value. To repeat a key pass a collection, or add more parameters with the same key in one call:

```c#
.Parameter("id", new[] { 1, 2, 3 })               // ?id=1&id=2&id=3
.Parameter(p =>
{
    p.Add(new RestParameter("tag", "a"));
    p.Add(new RestParameter("tag", "b"));         // ?tag=a&tag=b
})
```

### Post

POST is used to send data to a server for create/update a resource.
The data sent to the server with POST is stored in the request payload of the HTTP request:

```c#
var rest = Rest.Build();

var result = rest
    .Url("[URL]")
    .Command("/action")
    .Payload(new Object{})
    .Post<ResponseObject>();

var result = await rest
    .Url("[URL]")
    .Command("/action")
    .Payload(new Object{})
    .PostAsync<ResponseObject>();
```
Post is another common http method, wich is used to:

* POST requests are never cached
* POST requests do not remain in the browser history
* POST requests cannot be bookmarked
* POST requests have no restrictions on data length

### Put

PUT is used to send data to a server for create/update a resource.

The difference between POST and PUT is that PUT requests are idempotent. That is, calling the same PUT request multiple times will always produce the same result. In contrast, calling a POST request repeatedly have side effects of creating the same resource multiple times.

```c#
var rest = Rest.Build();

var result = rest
    .Url("[URL]")
    .Command("/action")
    .Payload(new Object{})
    .Put<ResponseObject>();

var result = await rest
    .Url("[URL]")
    .Payload(new Object{})
    .PutAsync<ResponseObject>();
```

### Delete

The DELETE method deletes the specified resource.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .Payload(new Object{})
    .Delete<ResponseObject>();

var result = await Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .Payload(new Object{})
    .DeleteAsync<ResponseObject>();
```

### Download

To download a resource use `byte[]` (in memory) or `Stream` (streamed, for large files):

```c#
var bytes = await Rest.Build().Url("[URL]").GetAsync<byte[]>();

using var file = await Rest.Build()
    .Url("[URL]")
    .OnDownloadProgress((d) => Console.WriteLine($"{d.CurrentBytes}/{d.TotalBytes}"))
    .GetAsync<Stream>();

using var output = File.Create("file.zip");
await file.Content!.CopyToAsync(output);   // OnDownloadProgress is raised while copying
```

### CancellationToken

Propagates notification that operations should be canceled.

A CancellationToken enables cooperative cancellation between threads, thread pool work items, or Task objects. 
You create a cancellation token by instantiating a CancellationTokenSource object, 
which manages cancellation tokens retrieved from its CancellationTokenSource.

The following example uses cancellation token to stop execution:

```c#
// Define the cancellation token.
using CancellationTokenSource source = new CancellationTokenSource();

// Schedules a cancel operation after the specified number of milliseconds
source.CancelAfter(3000);

var file1 = Rest.Build().Url("[URL FILE1]").GetAsync<byte[]>(source.Token);
var file2 = Rest.Build().Url("[URL FILE2]").GetAsync<byte[]>(source.Token);
var get1 = Rest.Build().Url("[URL GET1]").GetAsync<MyObject>(source.Token);

await Task.WhenAll(file1, file2, get1);
```

The request does not throw: the `OperationCanceledException` is available in `result.Exception` and
`result.IsCancellationRequested` is true. The synchronous methods accept a `CancellationToken` too.

### Retry

Transient errors (network errors, timeouts, 408, 429, 500, 502, 503, 504) can be retried with an exponential backoff
with jitter. The `Retry-After` header is honored (up to 30 seconds).

```c#
var result = await Rest.Build()
    .Url("[URL]")
    .Retry(3)                                   // 3 retries: 200ms, 400ms, 800ms (+ jitter)
    .GetAsync<MyObject>();

var count = result.RetryCount;                  // retries executed
```

Only idempotent methods (GET, HEAD, OPTIONS, PUT, DELETE, TRACE) are retried, because a POST or a PATCH could be executed twice.
Use `Retry(3, TimeSpan.FromSeconds(1), retryNonIdempotentMethods: true)` if your API is idempotent.
The timeout applies to every attempt. For advanced policies (circuit breaker, hedging) use `IHttpClientFactory` with
`Microsoft.Extensions.Http.Resilience`, see [Dependency Injection](#dependency-injection).

### Payload

RestClient uses Playload<T>(obj) method to set an object on request: 

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .Payload(new RequestObject{})
    .Post<ResponseObject>();
```

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .Payload(new RequestObject{})
    .Put<ResponseObject>();
```

### Form Url Encoded (application/x-www-form-urlencoded)

When necessary we can send the request as a form-url-encoded, passing the parameters as a dictionary
(`FormUrlEncoded(...)` enables the form-url-encoded content automatically):

```c#
var values = new Dictionary<string, string>();
values.Add("key1", "value1");
values.Add("key2", "value2");

var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .FormUrlEncoded(values)
    .Post();
```

It is possible to pass the parameters inside the handler, and to explicitly enable or disable the form-url-encoded:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .FormUrlEncoded(true, (p) =>
    {
        p.Add("key1", "value1");
        p.Add("key2", "value2");
    })
    .Post();
```

### Multipart (multipart/form-data)

To upload files and fields use `Multipart`:

```c#
var result = await Rest.Build()
    .Url("[URL]")
    .Command("/upload")
    .OnUploadProgress(p => Console.WriteLine($"{p.ProgressPercentage}%"))
    .Multipart(m => m
        .Add("title", "My document")
        .AddFile("document", "C:\\docs\\report.pdf", "application/pdf")   // opened when the request is sent
        .AddFile("thumbnail", thumbnailBytes, "thumb.png", "image/png")
        .AddFile("data", stream, "data.csv", "text/csv"))                 // not disposed by RestClient
    .PostAsync<UploadResponse>();
```

The content is created again for every attempt (retry, refresh token): byte arrays, file paths and stream factories
(`AddFile(name, () => OpenStream(), fileName)`) can always be resent, a `Stream` only if it is seekable.

### OnUploadProgress

OnUploadProgress occurs when the request is running and the data is going out. We can get a percentage of the data being uploaded like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnUploadProgress(p =>
    {
      DoSomethings(p.ProgressPercentage); 
    }) //occurs during request
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnDownloadProgress

OnDownloadProgress occurs when the response is running and the data is coming in. We can get a percentage of the data being downloading like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnDownloadProgress(p =>
    {
      DoSomethings(p.ProgressPercentage); 
    }) //occurs during response
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### Timeout

The default value is 100,000 milliseconds (100 seconds).
To set an infinite timeout, set the property value to InfiniteTimeSpan.
A Domain Name System (DNS) query may take up to 15 seconds to return or time out. If your request contains a host name that requires resolution and you set Timeout to a value less than 15 seconds, it may take 15 seconds or more before an Exception is thrown to indicate a timeout on your request.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Timeout(3200) //milliseconds
    .Get<ResponseObject>();
```

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Timeout(TimeSpan.FromMinutes(10)) 
    .Get<ResponseObject>();
```

When the timeout elapses the result contains a `TimeoutException` and `result.IsTimedOut` is true.
When the request is cancelled by a `CancellationToken` (or by `OnStart`) `result.IsCancellationRequested` is true.

### Handling the result

Requests never throw: network errors, timeouts and cancellations are reported in the result.

```c#
var result = await Rest.Build().Url("[URL]").GetAsync<User>();

if (result.IsSuccessStatusCode)          // 2xx and no exception
{
    var user = result.Content;
}
else if (result.Response != null)        // a response was received
{
    var status = result.StatusCode;      // e.g. 404
    var error = result.Exception;        // not null if the body could not be deserialized
}
else                                     // no response: network error, timeout, cancellation
{
    var error = result.Exception;
}

// or, if you prefer exceptions:
result.EnsureSuccessStatusCode();
```

### Logging

Pass an `ILogger` to log the requests (Debug), the responses (Information), the retries (Warning) and the errors (Error).
The query string is never logged because it can contain secrets.

```c#
var result = await Rest.Build()
    .Url("[URL]")
    .Logger(loggerFactory.CreateLogger("MyApi"))
    .GetAsync<MyObject>();
```

### Tracing (OpenTelemetry)

Every request creates an `Activity` named `RestClient {METHOD}` from the `ActivitySource` `Mafe.RestClient`, with the tags
`http.request.method`, `url.full` (without query string), `http.response.status_code`, `rest_client.retry_count` and
`error.type`. The activities of `HttpClient` (one for each attempt) are its children.

```c#
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
    .AddSource(RestClientTelemetry.ActivitySourceName)
    .AddHttpClientInstrumentation());
```

### OnStart

OnStart is an event that triggers when the request starts.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnStart((e) => { 
        DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnPreviewContentRequestAsString

OnPreviewContentRequestAsString is an event that triggers when the request is ready and it isn't no sent yet.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnPreviewContentRequestAsString((e) => { 
        DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnPreviewContentResponseAsString

OnPreviewContentResponseAsString is an event that triggers when the response is received and it isn't no deserialized yet.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnPreviewContentResponseAsString((e) => { 
        DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnPreCompleted

OnPreCompleted occurs when the request is completing but still hasn't completed yet. 
When OnPreCompleted is raised we can do something, for example get and use the result of request.

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnPreCompleted((r) => { 
        DoSomethings(r.Result); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnException

OnException occurs when the request raises an exception. 

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnException((e) => { 
      DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnCompleted

OnCompleted occurs when the request is completed. 

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .OnCompleted((e) => { 
        DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### Complete code example

RestClient allows to create a flexible and robust network layer and it is very easy to use.
Below you find a complete code demostration a complete code example. 

```c#
public class NetworkService {
    //building context

    public RestBuilder Root() 
        => Rest.Build()
            .CertificateValidation((sender, certificate, chain, errors) =>
            {
              if (development) return true; 
              return errors == SslPolicyErrors.None 
                      && validCerts.Contains(certificate.GetCertHashString()); 
            })
            .Url("[URL]");
    
    public RestBuilder RootAuthentication() 
        => Root()
             .Authentication(() => new AuthenticationHeaderValue("Bearer", "[Token]"))
             .RefreshToken()
             .RefreshTokenInvoke(async () => await PostRefreshAsync(new RefreshRequest { }));
    
    public RestBuilder UsersRoot() 
        => Root().Command("/Users");
    
    public RestBuilder DimensionsRoot() 
        => Root().Command("/Dimensions");
    
    public RestBuilder EventsRoot()
        => RootAuthentication().Command("/Events");
    
    //requests

    public async Task<RestResult<LoginResponse>> PostLoginAsync(LoginRequest request) 
        => await UsersRoot()
            .Command("/Login") //[URL]/Users/Login 
            .Payload(request)
            .PostAsync<LoginResponse>();
    
     public async Task<RestResult<RuleResponse>> GetRulesAsync() 
        => await UsersRoot()
            .Command("/Rules")
            .GetAsync<RuleResponse>();
    
    public async Task<RestResult<RefreshResponse>> PostRefreshAsync(RefreshRequest request) 
        => await UsersRoot()
            .Command("/Refresh")
            .Payload(request)
            .PostAsync<RefreshResponse>();
    
    public async Task<RestResult<CountryResponse>> PostCountriesAsync(CountryRequest request) 
        => await DimensionsRoot()
            .Command("/Countries")
            .Payload(request)
            .PostAsync<CountryResponse>();

    public async Task<RestResult<EventDetailResponse>> PostEventDetailAsync(EventDetailRequest request) 
        => await EventsRoot()
            .Command("/EventDetail")
            .Payload(request)
            .PostAsync<EventDetailResponse>();
}
```

## Migrating to 2.0

Version 2.0.0 modernizes the library and fixes several long-standing issues. It contains the following breaking changes.

### Target frameworks

* Supported: `net10.0`, `net8.0`, `netstandard2.1`, `netstandard2.0`.
* Removed (out of support): `net7.0`, `net6.0`, `netcoreapp3.1`, `netstandard1.3`, `net452`, `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`.
  .NET Framework 4.6.2+ applications keep working through `netstandard2.0`.
* Newtonsoft.Json is no longer used: JSON is always System.Text.Json. `Json(JsonSerializerSettings)` has been removed; use `Json(JsonSerializerOptions)` or a custom `ISerializerContent`.
* New dependencies: `Microsoft.Extensions.Logging.Abstractions` (and, on .NET Standard, `System.Text.Json` and `System.Diagnostics.DiagnosticSource`).

### Namespaces

`RestResult<T>` moved from `RestClient.Generic` to `RestClient`: remove `using RestClient.Generic;`.

### Removed APIs

| Removed | Use instead |
|---|---|
| `GetAsByteArray()`, `PostAsByteArrayAsync()`, ... (all `*AsByteArray*`) | `Get<byte[]>()`, `PostAsync<byte[]>()`, ... |
| `GetAsStream()`, `PostAsStreamAsync()`, ... (all `*AsStream*`) | `Get<Stream>()`, `PostAsync<Stream>()`, ... (now really streamed: dispose the result) |
| `Download(url)`, `DownloadAsync(url)` | `Url(url).GetAsync<byte[]>()` |
| `CustomCall(method)`, `CustomCallAsync<T>(method)`, ... | `Send<string>(method)`, `SendAsync<T>(method)`, ... |
| `Get(string url)`, `Get(Uri url)` | `Url(url).Get()` |
| `Command(uint)` | `Command(long)` (an `uint` argument still compiles) |
| `OnAuthentication(...)` | `Authentication(...)` |
| `Compression()` | `EnableGZipCompression()` |
| `EnableGZipCompression(bool deflate)` | `EnableGZipCompression()` (gzip, deflate and brotli are all enabled) |
| `OnPreResult(...)` | `OnPreCompleted(e => e.Result)` |
| `OnPreviewContentAsString(...)` | `OnPreviewContentResponseAsString(...)` |
| `RestResult<T>.StringContent` | `OnPreviewContentResponseAsString(...)` |
| `HttpClientCompressionHandler` | automatic decompression of the pooled handlers |
| `ProgressBytesChangedEventHandler` | `OnUploadProgress` / `OnDownloadProgress` |

### Changed signatures

* `GetAsync<T>()` with `T = string` returns the raw body (in 1.x it was deserialized as a JSON string).
* The synchronous methods (`Get()`, `Post<T>()`, ...) accept an optional `CancellationToken`.
* `CertificateValidation` takes `Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>` (existing lambdas compile unchanged).
* `NetworkCredential(Func<ICredentials?>)` instead of `Func<NetworkCredential>` (existing lambdas compile unchanged).
* `RestBuilder.Properties` returns a copy of the properties (in 1.x changing it changed every builder sharing the instance).
* `RestResult<T>` is sealed and it can no longer be created or subclassed outside the library.
* The whole API is annotated for nullable reference types.

### Behavior changes

* **Connections**: the HTTP handlers are pooled and the connections are reused (in 1.x a new handler, and a new TCP/TLS connection,
  was created for every request). Cookies are not stored, as in 1.x.
* **TLS certificate validation**: 1.x set `ServicePointManager.ServerCertificateValidationCallback` to *accept every certificate*
  for the whole process on .NET Framework/.NET Standard, as soon as a `RestBuilder` was created. This has been removed:
  certificates are validated by default and `CertificateValidation(...)` only affects the requests of that builder.
* **Immutable builder**: in 1.x commands and parameters were shared between a builder and the builders derived from it,
  so reusing a "root" builder could accumulate url segments. Now each call returns an independent copy.
  `Rest.Build(RestProperties)` copies the properties.
* **JSON defaults**: when no options are passed, JSON uses `JsonSerializerDefaults.Web`: payloads are serialized in **camelCase**
  and responses are read case-insensitively. To keep the 1.x behavior use `.Json(new JsonSerializerOptions())`.
* **Query string**: keys and values are url-encoded and formatted with the invariant culture. If you encoded them manually,
  remove the manual encoding. A trailing `/` in the endpoint is removed before appending the commands.
  Collections are expanded into repeated keys (`?id=1&id=2`).
* **Headers**: `Header(...)` and `Authentication(...)` are applied to the request message instead of `HttpClient.DefaultRequestHeaders`.
  Multiple `Header(...)` calls are combined (in 1.x the last one won).
* **FormUrlEncoded**: `FormUrlEncoded(dictionary)` and `FormUrlEncoded(action)` enable the form-url-encoded content automatically.
  `Payload`, `FormUrlEncoded` and `Multipart` replace each other.
* **Timeout**: the timeout is managed by RestClient and applies to every attempt. When it elapses the result contains a `TimeoutException`
  (`IsTimedOut == true`, `IsCancellationRequested == false`). For `Stream` results it covers the response headers only.
* **Streams**: `Stream` results are not buffered anymore: the connection stays open until the result (or the stream) is disposed.
* **RestResult**:
  * `StatusCode` is `0` (instead of `404 NotFound`) when no response has been received.
  * `Version` is `null` (in 1.x it threw a `FormatException`) and `ReasonPhrase` is the exception message when no response has been received.
  * `Exception` is available on the base `RestResult` class.
  * When the response body cannot be deserialized, the result keeps `Response`, `StatusCode` and `Headers` and the error is in `Exception`
    (in 1.x the whole response was lost). `IsSuccessStatusCode` is false in this case. An empty body is a `null` content, not an error.
  * `IRestResult.Content` returns the content (in 1.x it was always null).
  * Disposing a `RestResult<Stream>` disposes the stream.
* **Refresh token**: the refresh func is accepted when it returns any 2xx status code (not only 200). It runs once for all the concurrent
  requests that receive 401, the retry happens once per request, and the builder can be reused for later requests
  (in 1.x, after the first refresh the builder never refreshed again).
* **Generic methods**: the `where T : new()` constraint has been removed, so interfaces, arrays, records and immutable types can be used.
* **Sync methods** (`Get()`, `Post()`, ...) no longer deadlock in UI or classic ASP.NET synchronization contexts.
* **XML**: `Xml(readerOptions, writerOptions)` used the reader settings to serialize and the writer settings to deserialize; this has been fixed.
* `Parameter(RestParameter, ...)` no longer throws when the key already exists: the value is replaced.

### New APIs

* `SendAsync<T>(HttpMethod)` / `Send<T>(HttpMethod)`, `Head`, `Options`; `T` can be `string`, `byte[]`, `Stream` or a DTO.
* `Retry(maxRetries, delay, retryNonIdempotentMethods)` and `RestResult.RetryCount`.
* `Multipart(m => m.Add(...).AddFile(...))`.
* `Logger(ILogger)` and the `ActivitySource` `RestClientTelemetry.ActivitySourceName`.
* `Json(JsonSerializerContext)` for Native AOT, `IAsyncSerializerContent` for streaming deserialization.
* `Rest.Build(HttpClient)` and `UseHttpClient(HttpClient)` to reuse an external `HttpClient`.
* Package `Mafe.RestClient.Extensions.DependencyInjection`: `AddRestClient(name, ...)`, `AddRestClient<TClient>(...)`, `IRestClientFactory`.
* `Header(string name, string value)`, `Command(long)`, `CustomSerializer(serializer, options)`, `new RestParameter(key, value)`.
* `RestResult.EnsureSuccessStatusCode()`, `RestResult.IsTimedOut`.
* `RestProperties.Clone()`, `RestProperties.DefaultTimeout`, `RestProperties.DefaultBufferSize`, `JSON.DefaultOptions`.

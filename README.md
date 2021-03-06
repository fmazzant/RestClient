# RestClient

RestClient library provides a simple connector to consume REST services.

To use it in your project, add the Mafe.RestClient NuGet package to your project.

[![Nuget](https://img.shields.io/nuget/v/Mafe.RestClient?style=flat-square)](https://www.nuget.org/packages/Mafe.RestClient)

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
    .Url("[URL]")
    .Get();
```

### Serialization

Two types of serialization are supported by RestClient: Xml and Json, but it is possible to implementate ISerializerContent to customize the serialization.
RestClient uses .Json() to serialize an object into json.

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

The above snippet code consideres using System.Text.Json library. If we using Netwnsoft like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Json(new JsonSerializerSettings {
        Formatting = Formatting.Indented
    })
    .Get<ResponseObject>();
```  

RestClient uses .Xml() to serialize an object into xml.

```c#
var result = rest
    .Url("[URL]")
    .Xml()
    .Get<ResponseObject>();
```

It is possible to pass the settings to .Xml() method, like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Xml(new XmlReaderSettings { Indent = true }, new XmlWriterSettings { IgnoreWhitespace = true })
    .Get<ResponseObject>();
```

### Custom Serialization

Below an example on how you can do a custom Serialization by implementing the ISerializerContent interface:

```c#
public class MyCustomSerializer : ISerializerContent
{
    public string MediaTypeAsString => throw new NotImplementedException();

    public object DeserializeObject(string value, Type typeOf, object options = null)
    {
        throw new NotImplementedException();
    }

    public string SerializeObject(object value, Type typeOf, object options = null)
    {
        throw new NotImplementedException();
    }
}
```
Now, we can use MyCustomSerializer like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .CustomSerializer(new MyCustomSerializer { })
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

The library uncompresses automatically the response.

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

The DOWNLOAD method download the specified resource.

```c#
var result = Rest.Build()
    .Download("[URL]");

var result = await rest
    .DownloadAsync("[URL]");
```
Is possible display the download status with OnDownloadProgress.

```c#
var rest = Rest.Build();

var result = await rest
    .OnDownloadProgress((d) => Console.WriteLine($"{d.CurrentBytes}/{d.TotalBytes}"))
    .DownloadAsync("[URL]");
```

### CancellationToken

Propagates notification that operations should be canceled.

A CancellationToken enables cooperative cancellation between threads, thread pool work items, or Task objects. 
You create a cancellation token by instantiating a CancellationTokenSource object, 
which manages cancellation tokens retrieved from its CancellationTokenSource.

The following example uses cancellation token to stop execution:

```c#
// Define the cancellation token.
CancellationTokenSource source = new CancellationTokenSource();
CancellationToken token = source.Token;

// Schedules a cancel operation on this System.Threading.CancellationTokenSource
// after the specified number of milliseconds
token.CancelAfter(3000);

var file1 = Rest.Build().DownloadAsync("[URL FILE1]", token.Token);
var file2 = Rest.Build().DownloadAsync("[URL FILE2]", token.Token);
var get1 = Rest.Build().Url("[URL GET1]").GetAsync<MyObject>(token.Token);

Task.WaitAll(file1, file2, get1);
```

After cancellation requested it throws a TaskCancellatedException. 
The exception will be encapsulated into RestResult object.

### Custom Call

The CUSTOM method customizing the specified resource.

```c#
HttpMethod PATCH = new HttpMethod("PATCH");
var rest = Rest.Build();

var result = rest
    .Url("[URL]")
    .Command("/action")
    .Payload(new Object{})
    .CustomCall<ResponseObject>(PATCH);

var result = await rest
    .Url("[URL]")
    .Command("/action")
    .Payload(new Object{})
    .CustomCallAsync<ResponseObject>(PATCH);
```

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

When necessary we can use the request as a form-url-encoded. To use it we need to enabled it, like this:

```c#
var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .EnableFormUrlEncoded(true)
```

and then we can pass the parameters as a dictionary:

```c#
 var params = new Dictionary<string, string>();
 params.Add("key1", "value1");
 params.Add("key2", "value2");

var result = Rest.Build()
    .Url("[URL]")
    .Command("/action")
    .EnableFormUrlEncoded(true)
    .FormUrlEncoded(params)
    .Post();
```

It is possible to pass the parameters inside the handler and enabling the form-url-encoded:

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

### OnPreviewContentAsString -> OnPreviewContentResponseAsString (renaming)

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

### OnPreResult -> OnPreCompleted (renaming)

OnPreResult occurs when the request is completing but  still hasn't completed yet. 
When OnPreResult is raises we can todo somethings, for example  get and use the result of request.

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

### OnCompleted (changed type of parameter, from EventArgs to CompletedEventArgs)

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


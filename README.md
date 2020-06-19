# RestClient
RestClient library provides a simple connector to consume REST services.

Use Build() method to create a RestBuilder from Rest

```c#
var rest = Rest.Build();
```

With RestBuilder we can create a simply "Get" call:

```c#
var result = rest
  .Url("[URL]")
  .Get(); 
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
var result = rest
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
var result = rest
    .OnAuthentication(() => new AuthenticationHeaderValue("Bearer", "[Token]"))
    .Url("[URL]")
    .Get();
```

### Refresh Token

A valid bearer token (with active access_token or refresh_token properties) keeps the user's authentication alive without requiring him or her to re-enter their credentials frequently.
The access_token can be used for as long as it’s active, which is up to one hour after login or renewal. The refresh_token is active for 336 hours (14 days). After the access_token expires, an active refresh_token can be used to get a new access_token / refresh_token pair as shown in the following example. This cycle can continue for up to 90 days after which the user must log in again. If the refresh_token expires, the tokens cannot be renewed and the user must log in again.

To refresh a token, use "RefreshTokenInvoke" automatically.

```c#
var url = "[URL]";
var result = rest
    .OnAuthentication(() => new AuthenticationHeaderValue("Bearer", "[Token]"))
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
var result = rest
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

The Headers collection contains the protocol headers associated with the request. The .Header((h)=>{}) method allows to add the list of key.

```c#
var result = rest
    .Header((h) => {
        if(!h.Contains("auth-custom"))
            h.Add("auth-custom", "value");
    })
    .Url("[URL]")
    .Get();
```

### Serialization

Two types of serialization that are supported by RestClient: Xml and Json, but it is possible implemetate ISerializerContent to customize the serialization.
RestClient uses .Json() to use the json serialization.

```c#
var result = rest
    .Url("[URL]")
    .Json()
    .Get<MyObject>();
```

RestClient uses .Xml() to use the xml serialization.

```c#
var result = rest
    .Url("[URL]")
    .Xml()
    .Get<MyObject>();
```

### Custom Serialization

Consider the code below to demonstrate custom Serialization by implementing the ISerializerContent interface:

```c#
public class MyCustomSerializer : ISerializerContent
{
    public string MediaTypeAsString => throw new NotImplementedException();

    public object DeserializeObject(string value, Type typeOf)
    {
        throw new NotImplementedException();
    }

    public string SerializeObject(object value, Type typeOf)
    {
        throw new NotImplementedException();
    }
}
```
Now, we can use MyCustomSerializer how to explan the code below:

```c#
var result = rest
    .Url("[URL]")
    .CustomSerializer(new MyCustomSerializer { })
    .Get<MyObject>();
```

### Get

```c#
var result = rest
    .Url("[URL]")
    .Get();

var result = await rest
    .Url("[URL]")
    .GetAsync();
```

### Parameters

```c#
var result = rest
    .Url("[URL]")
    .Command("/path")
    .Parameter("id","10")
    .Parameter("type", "myType")
    .Get(); // [URL]/path?id=10&type=myTYpe

```

### Post

```c#
var result = rest
    .Url("[URL]")
    .Payload(new Object{})
    .Post<object>();

var result = await rest
    .Url("[URL]")
    .Payload(new Object{})
    .PostAsync<object>();
```

### Put

```c#
var result = rest
    .Url("[URL]")
    .Payload(new Object{})
    .Put<object>();

var result = await rest
    .Url("[URL]")
    .Payload(new Object{})
    .PutAsync<object>();
```

### Delete

```c#
var result = rest
    .Url("[URL]")
    .Payload(new Object{})
    .Delete<object>();

var result = await rest
    .Url("[URL]")
    .Payload(new Object{})
    .DeleteAsync<object>();
```

### Payload

```c#
var result = rest
    .Url("[URL]")
    .Payload(new RequestObject{})
    .Post<ResponseObject>();
```

```c#
var result = rest
    .Url("[URL]")
    .Payload(new RequestObject{})
    .Put<ResponseObject>();
```

### Progressing

```c#
var result = rest
    .Url("[URL]")
    .OnUploadProgress(p =>
    {
      DoSomethings(p.ProgressPercentage); 
    }) //occurs during request
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
var result = rest
    .Url("[URL]")
    .Timeout(3200) //milliseconds
    .Get<ResponseObject>();
```

```c#
var result = rest
    .Url("[URL]")
    .Timeout(TimeSpan.FromMinutes(10)) 
    .Get<ResponseObject>();
```

### OnStart

```c#
var result = rest
    .Url("[URL]")
    .OnStart((e) => { 
        DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```
### OnPreResult

```c#
var result = rest
    .Url("[URL]")
    .OnPreResult((e) => { 
        DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnCompleted

```c#
var result = rest
    .Url("[URL]")
    .OnCompleted((e) => { 
        DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### OnException

```c#
var result = rest
    .Url("[URL]")
    .OnException((e) => { 
      DoSomethings(e); 
    })
    .Payload(new BigObject{})
    .Post<ResponseObject>();
```

### Complete code example

```c#

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
             .OnAuthentication(() => new AuthenticationHeaderValue("Bearer", "[Token]"))
             .RefreshToken()
             .RefreshTokenInvoke(async () => await Refresh(new RefreshRequest { }));
    
    public RestBuilder UsersRoot() 
        => Root().Command("/Users");
    
    public RestBuilder DimensionsRoot() 
        => Root().Command("/Dimensions");
    
    public RestBuilder EventsRoot()
        => RootAuthentication().Command("/Events");
    
    //requests

    public async Task<RestResult<LoginResponse>> Login(LoginRequest request) 
        => await UsersRoot()
            .Command("/Login") //[URL]/Users/Login 
            .Payload(request)
            .PostAsync<LoginResponse>();
    
    public async Task<RestResult<RefreshResponse>> Refresh(RefreshRequest request) 
        => await UsersRoot()
            .Command("/Refresh")
            .Payload(request)
            .PostAsync<RefreshResponse>();
    
    public async Task<RestResult<CountryResponse>> Countries(CountryRequest request) 
        => await DimensionsRoot()
            .Command("/Countries")
            .Payload(request)
            .PostAsync<CountryResponse>();
    
    public async Task<RestResult<EventDetailResponse>> EventDetail(EventDetailRequest request) 
        => await EventsRoot()
            .Command("/EventDetail")
            .Payload(request)
            .PostAsync<EventDetailResponse>();
```


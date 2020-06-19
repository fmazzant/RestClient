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

```c#
var result = rest
    .OnAuthentication(() => new AuthenticationHeaderValue("Bearer", "[Token]"))
    .Url("[URL]")
    .Get();
```

### Network Credential

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

```c#
var result = rest
    .Url("[URL]")
    .Json()
    .Get<MyObject>();
```


```c#
var result = rest
    .Url("[URL]")
    .Xml()
    .Get<MyObject>();
```

### Custom Serialization

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

```c#
var result = rest
    .Url("[URL]")
    .CustomSerializer(new MyCustomSerializer { })
    .Get<MyObject>();
```

### Jwt Refresh Token


### Progressing


### Payload


### Post


### Put


### Delete


### Building example context

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
    
    public RestBuilder UsersRoot() 
        => Root().Command("/Users");
    
    public RestBuilder DimensionsRoot() 
        => Root().Command("/Dimensions");
    
    public RestBuilder EventsRoot() 
        => Root().Command("/Events");
    
    public RestBuilder EventsWithRefreshRoot() 
        => EventsRoot()
            .RefreshToken()
            .RefreshTokenInvoke(async () => await Refresh(new RefreshRequest { }));

    //execution

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
        => await EventsWithRefreshRoot()
            .Command("/EventDetail")
            .Payload(request)
            .PostAsync<EventDetailResponse>();
```


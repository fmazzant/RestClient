# RestClient
RestClient library provides a simple connector to consume REST services.

Use Build() method to create a RestBuilder from Rest

```c#
var rest = Rest.Build();
```

With RestBuilder we can create a simply "Get" call:

```c#
var result = rest
  .Url("https://www.federicomazzanti.com")
  .Get(); 
```

### Certificate Validation



```c#
var result = rest
    .CertificateValidation((object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) =>
    {
        if(develoment) return true;
        
    })
    .Url("https://www.federicomazzanti.com")
    .Get();
```
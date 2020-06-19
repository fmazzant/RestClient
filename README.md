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
      if (development) return true; // for development, trust all certificates
      return errors == SslPolicyErrors.None 
              && validCerts.Contains(certificate.GetCertHashString()); // Compliant: trust only some certificates
    })
    .Url("https://www.federicomazzanti.com")
    .Get();
```

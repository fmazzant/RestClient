# RestClient
RestClient library provides a simple connector to consume REST services. It is an extention of HttpClient object.

Use Rest() method to create a RestClientBuilder from RestClient

```c#
var rest = RestClient.Rest();
```

With RestClientBuilder we can create a simply "Get" call:

```c#
var result = rest
  .Url("https://www.federicomazzanti.com")
  .Get(); 
```

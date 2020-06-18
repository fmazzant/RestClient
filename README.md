# RestClient
RestClient library provides a simple connector to consume REST services. It is an extention of HttpClient object.

Use Rest() method to create a RestClientBuilder instance from HttpClient object

```c#
HttpClient httpClient = new HttpClient();
var rest = httpClient.Rest();
```

With RestClientBuilder we can create a simply "Get" call:

```c#
var result = rest
  .Url("https://www.federicomazzanti.com")
  .Get(); 
```

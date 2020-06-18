# RestClient
RestClient library provides a simple connector to consume REST services. It is an extention of HttpClient object.

In first time we create rest builder from HttpClient instance using Rest() method:
```c#
HttpClient httpClient = new HttpClient();
var rest = httpClient.Rest();
```

When rest biulder is craeted we can create a simple Get called, it is displayed in the following code:

```c#
var result = rest
  .Url("https://www.federicomazzanti.com")
  .Get(); 
```

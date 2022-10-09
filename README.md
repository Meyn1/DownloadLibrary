# DownloadLibrary
## _Shard HttpClient Library to handle Requests_


The Shard DownloadLibrary is an on .Net 6.0 based wrapper around the `HttpClient` to manage your HttpRequests. But it can also be used without the `HttpClient` to handle CPU intensive tasks.
All `Requests` will be handled by an `PriorityQueue` in a Parallel state to have a simple and efficient way to handle many (Tested with more than 500+) Http Requests.

- Easy to use!
- Efficient 
- ✨Contains file downloader! ✨

## Features
At the moment:
- **StatusRequest:** Calls a Head request and returns a response message with the header information.
- **OwnRequest:** Wrapper around your own requests. Easy to self-expand function of handling the downloads.
- **RequestContainer** A container class to merge requests together and to start, pause and await them.
- **Request:** Main abstract class that can be used to expand functionality on class-based level.
    - All subclasses have a retry function
    - A compleated and failed event
    - A priority funtcion
    - A second thread for bigger files to hold the app responsiv
    - Implementation for custom `CancellationToken` and a main `CancellationTokenSource` on `Downloader` to cancel all downloads
- **LoadRequest:** To download the response content into files.
  - This is a Http file downloader with this functions:
  - *Pause* and *Start* a download
  - *Resume* a download
  - Gets the *file name* and *extension* from the server 
  - Monitor the progress of the download with `IProgress<float>`
  - Can set path and filename 
  - Download a specified range of a file
  - Part a file into chunks but is not recommended (please help to update)
  - Exclude extensions for savety _(.exe; .bat.; etc...)_

> Expand and use as you like!

## Tech
Is available on GitHub:
Repository: https://github.com/Meyn1/DownloadLibrary

## Installation

Installation over [NuGet](https://www.nuget.org/packages/Shard.DonwloadLibrary) Packagemanager in Visual Studio or online.
URL: https://www.nuget.org/packages/Shard.DonwloadLibrary.
Package Manager Console: PM> NuGet\Install-Package Shard.DonwloadLibrary -Version 1.0.2

## How to use

Import the Library
```cs
using DownloaderLibrary.Requests;
```
Then create a new `Request` object like this `LoadRequest`
This `LoadRequest` downloads a file into the downloads folder of the PC with an ".part" file and uses the name that the server provides.
```cs
//To download a file and store it in "Downloads" folder
new LoadRequest("[Your URL]"); // e.g. https://speed.hetzner.de/100MB.bin
```
To set options on the `Request` create a `RequestOption` or for a `LoadRequest` a `LoadRequestOption`
```cs
// Create an option for a LoadRequest
  LoadRequestOptions requestOptions = new()
        {
            // Sets the filename of the download without the extension
            // The extension will be added automatically!
            FileName = "downloadfile", 
            // If this download has priority (default is false)
            PriorityLevel = PriorityLevel.High, 
            //(default is download folder)
            Path = "C:\\Users\\[Your Username]\\Desktop", 
            // If this Request contains a heavy request put it in second thread (default is false)
            IsDownload = true,
            //If the downloader sould Override, Create a new file or Append (default is Append)
            //Resume function only available with append!
            Mode = LoadMode.Create, 
            // Progress that writes the % to the Console
            Progress = new Progress<float>((percent => Console.WriteLine(percent + "%"))) 
        };
```
And use it in the Request
```cs
//To download a file and store it on the Desktop with a different name
new LoadRequest(" https://speed.hetzner.de/100MB.bin",requestOptions);
```
To wait on the Request use *await* or *WaitToFinish();*
```cs
await new LoadRequest(" https://speed.hetzner.de/100MB.bin",requestOptions).Task;
//new LoadRequest(" https://speed.hetzner.de/100MB.bin",requestOptions).WaitToFinish();
```
Create an `OwnRequest` like this:
```cs
    //Create an object that passes a CancellationToken
   new OwnRequest((downloadToken) =>
        {
            //Create your Request Massage. Here the body of google.com
            HttpRequestMessage requestMessage = new(HttpMethod.Get, "https://www.google.com");
            //Send your request and get the result. Pass the CancellationToken for handling it later over the Request object
            var response = DownloadHandler.Instance.SendAsync(requestMessage, downloadToken).Result;
            //If the resposne does not succeed
            if (!response.IsSuccessStatusCode)
                return false; // Return false to retry and call the failed method
            //If the response succeed. Do what you want and return to to finish the request
            Console.WriteLine("Finsihed");
            return true;
        });
```
To Create your own `Request` child. Here the implementation of the `OwnRequest` class:
```cs
    public class OwnRequest : Request
    {
        private readonly Func<CancellationToken, Task<bool>> _own;
        
        //Parent sets the Url field but doesn't need it and doesn't require a RequestOption because it creates then a new one.
        //But to use the options it have to be passed over to the parent
        public OwnRequest(Func<CancellationToken, Task<bool>> own, RequestOptions? requestOptions = null) : base(string.Empty, requestOptions)
        {
            _own = own;
            //Has to be called to inject it into the management process
            Start();
        }
        
        // Here will the Request be handled and a bool returned that indicates if it succeed
        protected override async Task<bool> RunRequestAsync()
        {
            bool result = await _own.Invoke(Token);
            if (result)
                Options.CompleatedAction?.Invoke(null);
            else
                Options.FaultedAction?.Invoke(new HttpResponseMessage());
            return result;
        }
    }
```
## License

MIT

## **Free Code** and **Free to Use**
#### Have fun!

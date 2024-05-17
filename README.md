# Unity Utilities

## Requirement 
- [UniTask](https://github.com/Cysharp/UniTask) 



### How to add submodule by command line
```bash
git submodule add  <url> <relative_path>
```
Example
```bash
git submodule add git@github.com:draftsama/unity-utilities.git Assets/Modules/unity-utilities
```

### UniTask Guide

#### Create Unity Event Handler Method
```csharp

UnityEvent _OnCompletedEvent = new UnityEvent();
public IUniTaskAsyncEnumerable<AsyncUnit> OnCompleted(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable(_OnCompletedEvent, _token);
        }

//--------------------------------------------
UnityEvent<Vector2> _OnChangedEvent = new UnityEvent<Vector2>();
public IUniTaskAsyncEnumerable<Vector2> OnCompleted(CancellationToken _token)
        {
            return new UnityEventHandlerAsyncEnumerable<Vector2>(_OnChangedEvent, _token);
        }


```


## APIs

### - FileLoader.DownloadFileAsync
#### Example
```cs
var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

var progress = new Progress<float>(percentage =>
{
    Debug.Log($"Download progress: {percentage * 100}%");
});

try
{
    var bytes = await FileLoader.DownloadFileAsync("https://example.com/myfile.txt", progress, cancellationToken);
    Debug.Log($"Download complete. Downloaded {bytes.Length} bytes.");
}
catch (OperationCanceledException ex)
{
    Debug.Log($"Download canceled: {ex.Message}");
}
```

### - FileLoader.LoadFileAsync
#### Example
```cs
var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

var progress = new Progress<float>(percentage =>
{
    Debug.Log($"Load progress: {percentage * 100}%");
});

try
{
    var bytes = await FileLoader.LoadFileAsync("image.png", progress, cancellationToken);
    Debug.Log($"Load complete. {bytes.Length} bytes.");
}
catch (OperationCanceledException ex)
{
    Debug.Log($"Canceled: {ex.Message}");
}
```
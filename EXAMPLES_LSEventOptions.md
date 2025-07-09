# LSEventOptions Usage Examples

This document demonstrates how to use the improved LSEventOptions API with multiple constructors, fluent methods, static factory methods, and extension methods.

## Basic Usage

### Simple Constructor Patterns

```csharp
// Default constructor
var options = new LSEventOptions();

// With success callback
var options = new LSEventOptions(
    onSuccess: () => Console.WriteLine("Operation completed")
);

// With success and failure callbacks
var options = new LSEventOptions(
    onSuccess: () => Console.WriteLine("Success!"),
    onFailure: (msg, cancelled) => Console.WriteLine($"Failed: {msg}")
);

// With dispatcher and callbacks
var options = new LSEventOptions(
    dispatcher: customDispatcher,
    onSuccess: () => Console.WriteLine("Operation completed"),
    onFailure: (msg, cancel) => Console.WriteLine($"Failed: {msg}"),
    errorHandler: (msg) => { Console.Error.WriteLine(msg); return true; }
);

// Copy constructor
var baseOptions = new LSEventOptions()
    .WithTimeout(10.0f)
    .WithDispatcher(customDispatcher);
var derivedOptions = new LSEventOptions(baseOptions)
    .WithSuccess(() => Console.WriteLine("Derived success"));
```

### Fluent API Methods

```csharp
// Chaining multiple configurations
var options = new LSEventOptions()
    .WithDispatcher(customDispatcher)
    .WithTimeout(5.0f)
    .WithSuccess(() => Console.WriteLine("Completed"))
    .WithFailure((msg, cancelled) => Console.WriteLine($"Failed: {msg}"))
    .WithCancel(() => Console.WriteLine("Cancelled"))
    .WithErrorHandler(msg => { 
        Console.Error.WriteLine($"Error: {msg}"); 
        return true; 
    })
    .WithGroupType(ListenerGroupType.SUBSET);

// Copy callbacks from another event
var childOptions = new LSEventOptions()
    .CopyCallbacksFrom(parentEvent)
    .WithDispatcher(customDispatcher);

// Copy callbacks from another options instance
var derivedOptions = new LSEventOptions()
    .CopyCallbacksFrom(baseOptions)
    .WithTimeout(customTimeout);
```

### Static Factory Methods

```csharp
// Create options for event chaining
var childOptions = LSEventOptions.ForEvent(parentEvent);
var childEvent = SomeOperation.Create(childOptions);
// When childEvent succeeds/fails, it will automatically signal parentEvent

// Create options with event callbacks and base options
var childOptions = LSEventOptions.WithEventCallbacks(parentEvent, standardOptions);
// childOptions inherits settings from standardOptions and chains to parentEvent

// Create options with specific dispatcher
var options = LSEventOptions.FromDispatcher(customDispatcher);

// Quick setup with basic callbacks
var options = LSEventOptions.Quick(
    onSuccess: () => Console.WriteLine("Done"),
    onFailure: (msg, cancelled) => Console.WriteLine($"Failed: {msg}")
);
```

## Instance Event Options (LSEventIOptions)

### Constructor Patterns

```csharp
// Default constructor (automatically sets SUBSET grouping)
var options = new LSEventIOptions();

// Copy from base options but ensure subset grouping
var options = new LSEventIOptions(baseOptions);

// With dispatcher and success callback
var options = new LSEventIOptions(customDispatcher, () => Console.WriteLine("Instance event completed"));
```

### Static Factory Methods

```csharp
// Factory method for chaining to parent events
var childOptions = LSEventIOptions.ForEvent(parentEvent, baseOptions);
var childEvent = SomeInstanceOperation.Create(specificInstance, childOptions);
// When childEvent succeeds/fails, it will automatically signal parentEvent
```

## Extension Methods

### Event Options Extensions

```csharp
// Chain to a target event
var options = new LSEventOptions()
    .WithTimeout(5.0f)
    .ChainTo(parentEvent);

// Inherit settings from another options instance
var childOptions = new LSEventOptions()
    .InheritFrom(parentOptions)
    .WithSuccess(() => Console.WriteLine("Child completed"));

// Convert to instance options
var staticOptions = LSEventOptions.Quick(() => Console.WriteLine("Done"));
var instanceOptions = staticOptions.ToInstanceOptions();
// instanceOptions has the same callbacks but uses SUBSET grouping
```

## Real-World Examples

### File Operation with Progress Tracking

```csharp
public class FileOperation {
    public static LSEvent ProcessFile(string filePath, LSEventOptions? options = null) {
        options ??= LSEventOptions.Quick(
            onSuccess: () => Console.WriteLine($"File {filePath} processed successfully"),
            onFailure: (msg, cancelled) => Console.WriteLine($"File processing failed: {msg}")
        );
        
        return new FileProcessEvent(filePath, options);
    }
}

// Usage
var fileOptions = new LSEventOptions()
    .WithTimeout(30.0f)
    .WithSuccess(() => UpdateUI("File processed"))
    .WithFailure((msg, cancel) => ShowError($"Processing failed: {msg}"));

var fileEvent = FileOperation.ProcessFile("document.txt", fileOptions);
fileEvent.Dispatch();
```

### Parent-Child Event Coordination

```csharp
public void ProcessMultipleFiles(string[] files) {
    var masterOptions = new LSEventOptions()
        .WithSuccess(() => Console.WriteLine("All files processed"))
        .WithFailure((msg, cancel) => Console.WriteLine($"Batch processing failed: {msg}"));
    
    var masterEvent = new BatchProcessEvent(masterOptions);
    
    foreach (var file in files) {
        // Each file operation will signal the master event when complete
        var fileOptions = LSEventOptions.ForEvent(masterEvent)
            .WithTimeout(10.0f);
        
        var fileEvent = FileOperation.ProcessFile(file, fileOptions);
        masterEvent.Wait(); // Add to batch
        fileEvent.Dispatch();
    }
}
```

### Instance-Based Event Handling

```csharp
public class PlayerManager {
    public void InitializePlayer(Player player) {
        var options = new LSEventIOptions()
            .WithSuccess(() => Console.WriteLine($"Player {player.Name} initialized"))
            .WithFailure((msg, cancel) => Console.WriteLine($"Player initialization failed: {msg}"))
            .WithTimeout(5.0f);
        
        var initEvent = OnInitializeEvent.Create(player, options);
        initEvent.Dispatch();
    }
}
```

This refactored API provides significantly better usability, readability, and flexibility while maintaining full backward compatibility with existing code.

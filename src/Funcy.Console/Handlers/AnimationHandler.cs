using System.Collections.Concurrent;
using Funcy.Console.Handlers.Models;
using Funcy.Core.Model;

namespace Funcy.Console.Handlers;

public class AnimationHandler : IAnimationProvider
{
    private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _hasAnimationsTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsTriggered { get; set; }
    
    private string[] _frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private int _frameIndex = 0;
    private int _frameduration = 100;
    
    public string CurrentFrame => _frames[_frameIndex];
    public string CurrentKey { get; set; } = string.Empty;
    
    private readonly ConcurrentDictionary<string, string> _animatedFunctions = [];
    
    public Task StartAsync(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (_animatedFunctions.IsEmpty)
                {
                    _hasAnimationsTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    await _hasAnimationsTcs.Task.WaitAsync(token);
                    if (token.IsCancellationRequested)
                        break;
                }

                _frameIndex = (_frameIndex + 1) % _frames.Length;
                foreach (var app in _animatedFunctions)
                {
                    _animatedFunctions[app.Key] = CurrentFrame;
                }
                IsTriggered = true;
                _tcs.TrySetResult();
                await Task.Delay(_frameduration, token);
            }
        }, token);
    }

    public List<AnimationContext> GetAnimations()
    {
        return _animatedFunctions.Select(x => new AnimationContext(x.Key, x.Value)).ToList();
    }

    public AnimationContext? GetAnimation(string key)
    {
        return _animatedFunctions.TryGetValue(key, out var frame) ? new AnimationContext(key, frame) : null;
    }

    public void AddAppDetails(string appDetailsKey)
    {
        var start = _animatedFunctions.IsEmpty;
        _animatedFunctions.TryAdd(appDetailsKey, CurrentFrame);
        if (start)
        {
            _hasAnimationsTcs.TrySetResult();
        }
    }
    
    public void RemoveAppDetails(string appDetailsKey)
    {
        _animatedFunctions.TryRemove(appDetailsKey, out _);
        if (_animatedFunctions.IsEmpty)
        {
            ResetTrigger();
        }
    }

    public Task WaitForTriggerAsync()
    {
        return _tcs.Task;
    }

    public void ResetTrigger()
    {
        IsTriggered = false;
        _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public interface IAnimationProvider
{
    List<AnimationContext> GetAnimations();
    AnimationContext? GetAnimation(string key);
}
#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;

namespace FTG_Framework.Tests;

// EventBus.Instance is process-global and serialized by EventBusTestCollection.
// EventBusTestScope owns baseline/reset responsibility; these helpers only capture
// typed events and process the frame requested by the scenario.
public static class EventBusTestHelper
{
    public static void Drain() => EventBus.Instance.ProcessFrame();

    public static List<T> Collect<T>(Action scenario)
    {
        Drain(); // Establish the capture boundary; EventBusTestScope owns test isolation.
        var events = new List<T>();
        void Handler(T e) => events.Add(e);
        EventBus.Instance.Subscribe<T>(Handler);
        try
        {
            scenario();
            EventBus.Instance.ProcessFrame();
        }
        finally
        {
            EventBus.Instance.Unsubscribe<T>(Handler);
        }
        return events;
    }

    public static (List<T1> first, List<T2> second) Collect<T1, T2>(Action scenario)
    {
        Drain(); // Establish the capture boundary; EventBusTestScope owns test isolation.
        var first = new List<T1>();
        var second = new List<T2>();
        void OnFirst(T1 e) => first.Add(e);
        void OnSecond(T2 e) => second.Add(e);
        EventBus.Instance.Subscribe<T1>(OnFirst);
        EventBus.Instance.Subscribe<T2>(OnSecond);
        try
        {
            scenario();
            EventBus.Instance.ProcessFrame();
        }
        finally
        {
            EventBus.Instance.Unsubscribe<T1>(OnFirst);
            EventBus.Instance.Unsubscribe<T2>(OnSecond);
        }
        return (first, second);
    }

    public static (List<T1> first, List<T2> second, List<T3> third) Collect<T1, T2, T3>(Action scenario)
    {
        Drain(); // Establish the capture boundary; EventBusTestScope owns test isolation.
        var first = new List<T1>();
        var second = new List<T2>();
        var third = new List<T3>();
        void OnFirst(T1 e) => first.Add(e);
        void OnSecond(T2 e) => second.Add(e);
        void OnThird(T3 e) => third.Add(e);
        EventBus.Instance.Subscribe<T1>(OnFirst);
        EventBus.Instance.Subscribe<T2>(OnSecond);
        EventBus.Instance.Subscribe<T3>(OnThird);
        try
        {
            scenario();
            EventBus.Instance.ProcessFrame();
        }
        finally
        {
            EventBus.Instance.Unsubscribe<T1>(OnFirst);
            EventBus.Instance.Unsubscribe<T2>(OnSecond);
            EventBus.Instance.Unsubscribe<T3>(OnThird);
        }
        return (first, second, third);
    }
}

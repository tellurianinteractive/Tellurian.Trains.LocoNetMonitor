﻿using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace Tellurian.Trains.LocoNetMonitor;
public class SlotTableUpdater : BackgroundService
{
    private readonly ILogger _logger;
    readonly IOptions<MonitorSettings> _options;

    readonly SlotTable _slots;
    readonly UdpClient _udpSendClient;
    readonly IPEndPoint _udpSendEndPoint;


    MonitorSettings Settings => _options.Value;

    public SlotTableUpdater(IOptions<MonitorSettings> options, SlotTable slots, ILogger<SlotTableUpdater> logger)
    {
        _options = options;
        _logger = logger;
        _slots = slots;
        _slots.SendToLocoNet = SendLocoNet;
        _slots.BlockUnassignedAdresses = Settings.LocoNet.BlockDrivingForUnassignedAdresses;
        _udpSendEndPoint = new IPEndPoint(IPAddress.Parse(Settings.Udp.BroadcastIPAddress), Settings.Udp.SendPort);
        _udpSendClient = new UdpClient
        {
            EnableBroadcast = true
        };
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{service} is starting.", nameof(SlotTableUpdater));
        var listenEndpoint = new IPEndPoint(IPAddress.Any, Settings.Udp.BroadcastPort);
        using var udpClient = new UdpClient(listenEndpoint);
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await udpClient.ReceiveAsync(stoppingToken);
            var slotNumber = _slots.Update(result.Buffer);

            if (slotNumber > 0) _logger.LogInformation("Updated: {slot}", _slots[slotNumber].ToString());
            else _logger.LogDebug("Ignored: {message}", result.Buffer.ToHex());
        }
    }

    private void SendLocoNet(byte[] data)
    {
        var count = _udpSendClient.Send(data, data.Length, _udpSendEndPoint);
        _logger.LogDebug("{service} sends data: {data}", nameof(SlotTableUpdater), data.ToHex());
    }
}
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Exceptions;
using Gress;

namespace DiscordChatExporter.Core.Exporting;

public class ChannelExporter
{
    private readonly DiscordClient _discord;

    public ChannelExporter(DiscordClient discord) => _discord = discord;

    public async ValueTask ExportChannelAsync(
        ExportRequest request,
        IProgress<Percentage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Build context
        var context = new ExportContext(_discord, request);
        await context.PopulateChannelsAndRolesAsync(cancellationToken);

        // Export messages
        await using var messageExporter = new MessageExporter(context);

        await foreach (var message in _discord.GetMessagesAsync(
                           request.Channel.Id,
                           request.After,
                           request.Before,
                           progress,
                           cancellationToken))
        {
            // Resolve members for referenced users
            foreach (var user in message.GetReferencedUsers())
                await context.PopulateMemberAsync(user, cancellationToken);

            // Export the message
            if (request.MessageFilter.IsMatch(message))
                await messageExporter.ExportMessageAsync(message, cancellationToken);
        }

        // Throw if no messages were exported
        if (messageExporter.MessagesExported <= 0)
            throw DiscordChatExporterException.ChannelIsEmpty();
    }
}
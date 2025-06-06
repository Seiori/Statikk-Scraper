﻿namespace Statikk_Scraper.Models;

public class Patches
{
    /// <summary>
    /// Fields
    /// </summary>
    public ushort Id { get; init; }
    public byte Season { get; init; }
    public required string PatchVersion { get; init; }

    /// <summary>
    /// Children
    /// </summary>
    public ICollection<Matches> Matches { get; init; } = [];
}
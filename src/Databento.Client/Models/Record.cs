namespace Databento.Client.Models;

/// <summary>
/// Base class for all Databento records
/// </summary>
public abstract class Record
{
    /// <summary>
    /// Timestamp in nanoseconds since Unix epoch
    /// </summary>
    public long TimestampNs { get; set; }

    /// <summary>
    /// Record type identifier
    /// </summary>
    public byte RType { get; set; }

    /// <summary>
    /// Publisher ID
    /// </summary>
    public ushort PublisherId { get; set; }

    /// <summary>
    /// Instrument ID
    /// </summary>
    public uint InstrumentId { get; set; }

    /// <summary>
    /// Get timestamp as DateTimeOffset
    /// </summary>
    public DateTimeOffset Timestamp =>
        DateTimeOffset.FromUnixTimeMilliseconds(TimestampNs / 1_000_000);

    /// <summary>
    /// Deserialize a record from raw bytes with the given RType
    /// </summary>
    internal static unsafe Record FromBytes(ReadOnlySpan<byte> bytes, byte rtype)
    {
        if (bytes.Length < 16)
            throw new ArgumentException($"Invalid record data - too small: {bytes.Length} bytes", nameof(bytes));

        // Read RecordHeader (16 bytes)
        // offset 0: length (uint8)
        // offset 1: rtype (uint8)
        // offset 2-3: publisher_id (uint16)
        // offset 4-7: instrument_id (uint32)
        // offset 8-15: ts_event (uint64 UnixNanos)

        ushort publisherId = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(2, 2));
        uint instrumentId = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        long tsEvent = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(8, 8));

        // Dispatch to appropriate record type based on RType
        Record result = (rtype, bytes.Length) switch
        {
            // Trade messages (48 bytes) - Mbp0 / Trades schema
            (0x00, 48) => DeserializeTradeMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // MBO messages (56 bytes)
            (0xA0, 56) => DeserializeMboMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // MBP-1 messages (80 bytes)
            (0x01, 80) => DeserializeMbp1Msg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // MBP-10 messages (368 bytes)
            (0x02, 368) => DeserializeMbp10Msg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // OHLCV messages (56 bytes) - multiple RType values
            (0x12, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1s
            (0x13, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1m
            (0x14, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1h
            (0x15, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1d
            (0x16, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // EOD

            // Status messages (40 bytes)
            (0x17, 40) => DeserializeStatusMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Instrument definition messages (520 bytes)
            (0x18, 520) => DeserializeInstrumentDefMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Imbalance messages (112 bytes)
            (0x19, 112) => DeserializeImbalanceMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Error messages (320 bytes)
            (0x1A, 320) => DeserializeErrorMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Symbol mapping messages (176 bytes)
            (0x1B, 176) => DeserializeSymbolMappingMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // System messages (320 bytes)
            (0x1C, 320) => DeserializeSystemMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Statistics messages (80 bytes)
            (0x1D, 80) => DeserializeStatMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // BBO messages (80 bytes) - multiple types
            (0xC2, 80) => DeserializeBboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Bbo1S
            (0xC3, 80) => DeserializeBboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Bbo1M

            // CBBO messages (80 bytes) - multiple types
            (0xB2, 80) => DeserializeCbboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Cbbo1S
            (0xB3, 80) => DeserializeCbboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Cbbo1M
            (0xB4, 80) => DeserializeCbboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Tcbbo

            // CMBP-1 messages (80 bytes)
            (0xB1, 80) => DeserializeCmbp1Msg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // System/metadata messages
            _ => new UnknownRecord { RType = rtype, RawData = bytes.ToArray() }
        };

        return result;
    }

    private static TradeMessage DeserializeTradeMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 48)
            throw new ArgumentException("Invalid TradeMsg data - too small", nameof(bytes));

        // TradeMsg layout (48 bytes):
        // offset 16-23: price (int64)
        // offset 24-27: size (uint32)
        // offset 28: action (uint8/char)
        // offset 29: side (uint8/char)
        // offset 30: flags (uint8)
        // offset 31: depth (uint8)
        // offset 32-39: ts_recv (uint64)
        // offset 40-43: ts_in_delta (int32)
        // offset 44-47: sequence (uint32)

        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4));
        Action action = (Action)bytes[28];
        Side side = (Side)bytes[29];
        byte flags = bytes[30];
        byte depth = bytes[31];
        uint sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(44, 4));

        return new TradeMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Price = price,
            Size = size,
            Action = action,
            Side = side,
            Flags = flags,
            Depth = depth,
            Sequence = sequence
        };
    }

    private static MboMessage DeserializeMboMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 56)
            throw new ArgumentException("Invalid MboMsg data - too small", nameof(bytes));

        // MboMsg layout (56 bytes):
        // offset 16-23: order_id (uint64)
        // offset 24-31: price (int64)
        // offset 32-35: size (uint32)
        // offset 36: flags (uint8)
        // offset 37: channel_id (uint8)
        // offset 38: action (uint8/char)
        // offset 39: side (uint8/char)
        // offset 40-47: ts_recv (uint64)
        // offset 48-51: ts_in_delta (int32)
        // offset 52-55: sequence (uint32)

        ulong orderId = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(16, 8));
        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(24, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(32, 4));
        byte flags = bytes[36];
        byte channelId = bytes[37];
        Action action = (Action)bytes[38];
        Side side = (Side)bytes[39];
        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(40, 8));
        int tsInDelta = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(48, 4));
        uint sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(52, 4));

        return new MboMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            OrderId = orderId,
            Price = price,
            Size = size,
            Flags = flags,
            ChannelId = channelId,
            Action = action,
            Side = side,
            TsRecv = tsRecv,
            TsInDelta = tsInDelta,
            Sequence = sequence
        };
    }

    private static Mbp1Message DeserializeMbp1Msg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 80)
            throw new ArgumentException("Invalid Mbp1Msg data - too small", nameof(bytes));

        // Mbp1Msg layout (80 bytes):
        // offset 16-23: price (int64)
        // offset 24-27: size (uint32)
        // offset 28: action (uint8/char)
        // offset 29: side (uint8/char)
        // offset 30: flags (uint8)
        // offset 31: depth (uint8)
        // offset 32-39: ts_recv (uint64)
        // offset 40-43: ts_in_delta (int32)
        // offset 44-47: sequence (uint32)
        // offset 48-79: levels[1] (BidAskPair - 32 bytes)

        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4));
        Action action = (Action)bytes[28];
        Side side = (Side)bytes[29];
        byte flags = bytes[30];
        byte depth = bytes[31];
        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        int tsInDelta = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(40, 4));
        uint sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(44, 4));

        // Deserialize BidAskPair
        BidAskPair level = DeserializeBidAskPair(bytes.Slice(48, 32));

        return new Mbp1Message
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Price = price,
            Size = size,
            Action = action,
            Side = side,
            Flags = flags,
            Depth = depth,
            TsRecv = tsRecv,
            TsInDelta = tsInDelta,
            Sequence = sequence,
            Level = level
        };
    }

    private static Mbp10Message DeserializeMbp10Msg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 368)
            throw new ArgumentException("Invalid Mbp10Msg data - too small", nameof(bytes));

        // Mbp10Msg layout (368 bytes): same as Mbp1 but with 10 levels (320 bytes)

        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4));
        Action action = (Action)bytes[28];
        Side side = (Side)bytes[29];
        byte flags = bytes[30];
        byte depth = bytes[31];
        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        int tsInDelta = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(40, 4));
        uint sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(44, 4));

        // Deserialize 10 BidAskPairs
        BidAskPair[] levels = new BidAskPair[10];
        for (int i = 0; i < 10; i++)
        {
            levels[i] = DeserializeBidAskPair(bytes.Slice(48 + i * 32, 32));
        }

        return new Mbp10Message
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Price = price,
            Size = size,
            Action = action,
            Side = side,
            Flags = flags,
            Depth = depth,
            TsRecv = tsRecv,
            TsInDelta = tsInDelta,
            Sequence = sequence,
            Levels = levels
        };
    }

    private static BidAskPair DeserializeBidAskPair(ReadOnlySpan<byte> bytes)
    {
        // BidAskPair layout (32 bytes):
        // offset 0-7: bid_px (int64)
        // offset 8-15: ask_px (int64)
        // offset 16-19: bid_sz (uint32)
        // offset 20-23: ask_sz (uint32)
        // offset 24-27: bid_ct (uint32)
        // offset 28-31: ask_ct (uint32)

        return new BidAskPair
        {
            BidPrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0, 8)),
            AskPrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(8, 8)),
            BidSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4)),
            AskSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(20, 4)),
            BidCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4)),
            AskCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(28, 4))
        };
    }

    private static OhlcvMessage DeserializeOhlcvMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 56)
            throw new ArgumentException("Invalid OhlcvMsg data - too small", nameof(bytes));

        // OhlcvMsg layout (56 bytes):
        // offset 16-23: open (int64)
        // offset 24-31: high (int64)
        // offset 32-39: low (int64)
        // offset 40-47: close (int64)
        // offset 48-55: volume (uint64)

        long open = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        long high = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(24, 8));
        long low = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        long close = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(40, 8));
        ulong volume = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(48, 8));

        return new OhlcvMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume
        };
    }

    private static StatusMessage DeserializeStatusMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 40)
            throw new ArgumentException("Invalid StatusMsg data - too small", nameof(bytes));

        // StatusMsg layout (40 bytes):
        // offset 16-23: ts_recv (uint64)
        // offset 24-25: action (uint16/StatusAction)
        // offset 26-27: reason (uint16/StatusReason)
        // offset 28: trading_event (uint8/TradingEvent)
        // offset 29: is_trading (char/TriState)
        // offset 30: is_quoting (char/TriState)
        // offset 31: is_short_sell_restricted (char/TriState)

        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        StatusAction action = (StatusAction)System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(24, 2));
        StatusReason reason = (StatusReason)System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(26, 2));
        TradingEvent tradingEvent = (TradingEvent)bytes[28];
        TriState isTrading = (TriState)bytes[29];
        TriState isQuoting = (TriState)bytes[30];
        TriState isShortSellRestricted = (TriState)bytes[31];

        return new StatusMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            TsRecv = tsRecv,
            Action = action,
            Reason = reason,
            TradingEvent = tradingEvent,
            IsTrading = isTrading,
            IsQuoting = isQuoting,
            IsShortSellRestricted = isShortSellRestricted
        };
    }

    private static InstrumentDefMessage DeserializeInstrumentDefMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 520)
            throw new ArgumentException("Invalid InstrumentDefMsg data - too small", nameof(bytes));

        // InstrumentDefMsg layout (520 bytes) - very large, many fields
        // This is a simplified deserialization of the most important fields

        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        long minPriceIncrement = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(24, 8));
        long displayFactor = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        long expiration = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(40, 8));
        long activation = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(48, 8));
        long highLimitPrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(56, 8));
        long lowLimitPrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(64, 8));
        long maxPriceVariation = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(72, 8));
        long tradingReferencePrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(80, 8));

        // Read string fields (null-terminated C strings)
        string currency = ReadCString(bytes.Slice(178, 5));
        string settlCurrency = ReadCString(bytes.Slice(183, 5));
        string secSubType = ReadCString(bytes.Slice(188, 6));
        string rawSymbol = ReadCString(bytes.Slice(194, 22));
        string group = ReadCString(bytes.Slice(216, 21));
        string exchange = ReadCString(bytes.Slice(237, 5));
        string asset = ReadCString(bytes.Slice(242, 7));
        string cfi = ReadCString(bytes.Slice(249, 7));
        string securityType = ReadCString(bytes.Slice(256, 7));
        string unitOfMeasure = ReadCString(bytes.Slice(263, 31));
        string underlying = ReadCString(bytes.Slice(294, 21));

        InstrumentClass instrumentClass = (InstrumentClass)bytes[319];
        long strikePrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(320, 8));
        MatchAlgorithm matchAlgorithm = (MatchAlgorithm)bytes[328];

        return new InstrumentDefMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            TsRecv = tsRecv,
            MinPriceIncrement = minPriceIncrement,
            DisplayFactor = displayFactor,
            Expiration = expiration,
            Activation = activation,
            HighLimitPrice = highLimitPrice,
            LowLimitPrice = lowLimitPrice,
            MaxPriceVariation = maxPriceVariation,
            TradingReferencePrice = tradingReferencePrice,
            Currency = currency,
            SettlCurrency = settlCurrency,
            SecSubType = secSubType,
            RawSymbol = rawSymbol,
            Group = group,
            Exchange = exchange,
            Asset = asset,
            Cfi = cfi,
            SecurityType = securityType,
            UnitOfMeasure = unitOfMeasure,
            Underlying = underlying,
            InstrumentClass = instrumentClass,
            StrikePrice = strikePrice,
            MatchAlgorithm = matchAlgorithm
        };
    }

    private static ImbalanceMessage DeserializeImbalanceMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 112)
            throw new ArgumentException("Invalid ImbalanceMsg data - too small", nameof(bytes));

        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        long refPrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(24, 8));
        long auctionTime = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        ulong pairedQty = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(64, 8));
        ulong totalImbalanceQty = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(72, 8));
        Side side = (Side)bytes[96];

        return new ImbalanceMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            TsRecv = tsRecv,
            RefPrice = refPrice,
            AuctionTime = auctionTime,
            PairedQty = pairedQty,
            TotalImbalanceQty = totalImbalanceQty,
            Side = side
        };
    }

    private static ErrorMessage DeserializeErrorMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 320)
            throw new ArgumentException("Invalid ErrorMsg data - too small", nameof(bytes));

        string error = ReadCString(bytes.Slice(16, 302));
        byte code = bytes[318];
        bool isLast = bytes[319] != 0;

        return new ErrorMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Error = error,
            Code = code,
            IsLast = isLast
        };
    }

    private static SymbolMappingMessage DeserializeSymbolMappingMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 176)
            throw new ArgumentException("Invalid SymbolMappingMsg data - too small", nameof(bytes));

        SType stypeIn = (SType)bytes[16];
        string stypeInSymbol = ReadCString(bytes.Slice(17, 71));
        SType stypeOut = (SType)bytes[88];
        string stypeOutSymbol = ReadCString(bytes.Slice(89, 71));
        long startTs = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(160, 8));
        long endTs = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(168, 8));

        return new SymbolMappingMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            STypeIn = stypeIn,
            STypeInSymbol = stypeInSymbol,
            STypeOut = stypeOut,
            STypeOutSymbol = stypeOutSymbol,
            StartTs = startTs,
            EndTs = endTs
        };
    }

    private static SystemMessage DeserializeSystemMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 320)
            throw new ArgumentException("Invalid SystemMsg data - too small", nameof(bytes));

        string message = ReadCString(bytes.Slice(16, 303));
        byte code = bytes[319];

        return new SystemMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Message = message,
            Code = code
        };
    }

    private static StatMessage DeserializeStatMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 80)
            throw new ArgumentException("Invalid StatMsg data - too small", nameof(bytes));

        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        long tsRef = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(24, 8));
        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        long quantity = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(40, 8));
        uint sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(48, 4));
        int tsInDelta = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(52, 4));
        ushort statType = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(56, 2));
        ushort channelId = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(58, 2));
        byte updateAction = bytes[60];
        byte statFlags = bytes[61];

        return new StatMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            TsRecv = tsRecv,
            TsRef = tsRef,
            Price = price,
            Quantity = quantity,
            Sequence = sequence,
            TsInDelta = tsInDelta,
            StatType = statType,
            ChannelId = channelId,
            UpdateAction = updateAction,
            StatFlags = statFlags
        };
    }

    private static BboMessage DeserializeBboMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 80)
            throw new ArgumentException("Invalid BboMsg data - too small", nameof(bytes));

        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4));
        Side side = (Side)bytes[28];
        byte flags = bytes[29];
        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        uint sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(40, 4));
        BidAskPair level = DeserializeBidAskPair(bytes.Slice(48, 32));

        return new BboMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Price = price,
            Size = size,
            Side = side,
            Flags = flags,
            TsRecv = tsRecv,
            Sequence = sequence,
            Level = level
        };
    }

    private static CbboMessage DeserializeCbboMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 80)
            throw new ArgumentException("Invalid CbboMsg data - too small", nameof(bytes));

        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4));
        Side side = (Side)bytes[28];
        byte flags = bytes[29];
        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        uint sequence = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(40, 4));
        ConsolidatedBidAskPair level = DeserializeConsolidatedBidAskPair(bytes.Slice(48, 32));

        return new CbboMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Price = price,
            Size = size,
            Side = side,
            Flags = flags,
            TsRecv = tsRecv,
            Sequence = sequence,
            Level = level
        };
    }

    private static Cmbp1Message DeserializeCmbp1Msg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        if (bytes.Length < 80)
            throw new ArgumentException("Invalid Cmbp1Msg data - too small", nameof(bytes));

        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4));
        Action action = (Action)bytes[28];
        Side side = (Side)bytes[29];
        byte flags = bytes[30];
        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        int tsInDelta = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(40, 4));
        ConsolidatedBidAskPair level = DeserializeConsolidatedBidAskPair(bytes.Slice(48, 32));

        return new Cmbp1Message
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Price = price,
            Size = size,
            Action = action,
            Side = side,
            Flags = flags,
            TsRecv = tsRecv,
            TsInDelta = tsInDelta,
            Level = level
        };
    }

    private static ConsolidatedBidAskPair DeserializeConsolidatedBidAskPair(ReadOnlySpan<byte> bytes)
    {
        // ConsolidatedBidAskPair layout (32 bytes):
        // offset 0-7: bid_px (int64)
        // offset 8-15: ask_px (int64)
        // offset 16-19: bid_sz (uint32)
        // offset 20-23: ask_sz (uint32)
        // offset 24-25: bid_pb (uint16)
        // offset 26-27: ask_pb (uint16)

        return new ConsolidatedBidAskPair
        {
            BidPrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0, 8)),
            AskPrice = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(8, 8)),
            BidSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4)),
            AskSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(20, 4)),
            BidPublisher = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(24, 2)),
            AskPublisher = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(26, 2))
        };
    }

    private static string ReadCString(ReadOnlySpan<byte> bytes)
    {
        // Find null terminator
        int length = bytes.IndexOf((byte)0);
        if (length < 0) length = bytes.Length;

        // Convert to string, trimming any padding
        return System.Text.Encoding.UTF8.GetString(bytes.Slice(0, length)).TrimEnd('\0', ' ');
    }
}


/// <summary>
/// Placeholder for unknown record types
/// </summary>
public class UnknownRecord : Record
{
    public byte[]? RawData { get; set; }
}

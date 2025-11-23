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
    /// Raw DBN-format bytes for this record (if available)
    /// </summary>
    internal byte[]? RawBytes { get; set; }

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
            // Note: RType 0x01 is used for both MBP-1 and TBBO schemas (they're binary-identical)
            // TbboMessage exists as a type but can't be distinguished at deserialization without schema context
            (0x01, 80) => DeserializeMbp1Msg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // MBP-10 messages (368 bytes) - FIXED: was 0x02, correct is 0x0A
            (0x0A, 368) => DeserializeMbp10Msg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // OHLCV messages (56 bytes) - multiple RType values - FIXED: All values corrected
            (0x11, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Deprecated
            (0x20, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1s - FIXED: was 0x12
            (0x21, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1m - FIXED: was 0x13
            (0x22, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1h - FIXED: was 0x14
            (0x23, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // 1d - FIXED: was 0x15
            (0x24, 56) => DeserializeOhlcvMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // EOD - FIXED: was 0x16

            // Status messages (40 bytes) - FIXED: was 0x17, correct is 0x12
            (0x12, 40) => DeserializeStatusMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Instrument definition messages (520 bytes) - FIXED: was 0x18, correct is 0x13
            (0x13, 520) => DeserializeInstrumentDefMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Imbalance messages (112 bytes) - FIXED: was 0x19, correct is 0x14
            (0x14, 112) => DeserializeImbalanceMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Error messages (320 bytes) - FIXED: was 0x1A, correct is 0x15
            (0x15, 320) => DeserializeErrorMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Symbol mapping messages (176 bytes) - FIXED: was 0x1B, correct is 0x16
            (0x16, 176) => DeserializeSymbolMappingMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // System messages (320 bytes) - FIXED: was 0x1C, correct is 0x17
            (0x17, 320) => DeserializeSystemMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // Statistics messages (80 bytes) - FIXED: was 0x1D, correct is 0x18
            (0x18, 80) => DeserializeStatMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // CMBP-1 messages (80 bytes)
            (0xB1, 80) => DeserializeCmbp1Msg(bytes, rtype, publisherId, instrumentId, tsEvent), // Cmbp1

            // Tcbbo (Trade with Consolidated BBO) - 80 bytes, distinct RType
            (0xC2, 80) => DeserializeTcbboMsg(bytes, rtype, publisherId, instrumentId, tsEvent),

            // CBBO messages (80 bytes) - multiple types
            (0xC0, 80) => DeserializeCbboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Cbbo1S
            (0xC1, 80) => DeserializeCbboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Cbbo1M

            // BBO messages (80 bytes) - multiple types
            (0xC3, 80) => DeserializeBboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Bbo1S
            (0xC4, 80) => DeserializeBboMsg(bytes, rtype, publisherId, instrumentId, tsEvent), // Bbo1M

            // System/metadata messages
            _ => new UnknownRecord { RType = rtype, RawData = bytes.ToArray() }
        };

        // Store raw bytes for potential writing
        result.RawBytes = bytes.ToArray();

        return result;
    }

    private static TradeMessage DeserializeTradeMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 48;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid TradeMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 56;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid MboMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 80;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid Mbp1Msg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 368;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid Mbp10Msg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 56;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid OhlcvMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 40;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid StatusMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // DBN v2 InstrumentDefMsg: Exactly 520 bytes
        // Specification: https://docs.rs/dbn/latest/dbn/record/struct.InstrumentDefMsg.html
        // All offsets verified against databento-cpp record.hpp
        const int ExpectedSize = 520;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid InstrumentDefMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

        return new InstrumentDefMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,

            // int64 fields (offsets 16-135)
            TsRecv = ReadInt64(bytes, 16),
            MinPriceIncrement = ReadInt64(bytes, 24),
            DisplayFactor = ReadInt64(bytes, 32),
            Expiration = ReadInt64(bytes, 40),
            Activation = ReadInt64(bytes, 48),
            HighLimitPrice = ReadInt64(bytes, 56),
            LowLimitPrice = ReadInt64(bytes, 64),
            MaxPriceVariation = ReadInt64(bytes, 72),
            UnitOfMeasureQty = ReadInt64(bytes, 80),
            MinPriceIncrementAmount = ReadInt64(bytes, 88),
            PriceRatio = ReadInt64(bytes, 96),
            StrikePrice = ReadInt64(bytes, 104),           // FIXED: was at 320!
            RawInstrumentId = ReadRawInstrumentId(bytes, 112),
            LegPrice = ReadInt64(bytes, 120),              // NEW: Multi-leg field
            LegDelta = ReadInt64(bytes, 128),              // NEW: Multi-leg field

            // int32/uint32 fields (offsets 136-211)
            InstAttribValue = ReadInt32(bytes, 136),
            UnderlyingId = ReadUInt32(bytes, 140),
            MarketDepthImplied = ReadInt32(bytes, 144),
            MarketDepth = ReadInt32(bytes, 148),
            MarketSegmentId = ReadUInt32(bytes, 152),
            MaxTradeVol = ReadUInt32(bytes, 156),
            MinLotSize = ReadInt32(bytes, 160),
            MinLotSizeBlock = ReadInt32(bytes, 164),
            MinLotSizeRoundLot = ReadInt32(bytes, 168),
            MinTradeVol = ReadUInt32(bytes, 172),
            ContractMultiplier = ReadInt32(bytes, 176),
            DecayQuantity = ReadInt32(bytes, 180),
            OriginalContractSize = ReadInt32(bytes, 184),
            LegInstrumentId = ReadUInt32(bytes, 188),      // NEW: Multi-leg field
            LegRatioPriceNumerator = ReadInt32(bytes, 192),   // NEW: Multi-leg field
            LegRatioPriceDenominator = ReadInt32(bytes, 196), // NEW: Multi-leg field
            LegRatioQtyNumerator = ReadInt32(bytes, 200),     // NEW: Multi-leg field
            LegRatioQtyDenominator = ReadInt32(bytes, 204),   // NEW: Multi-leg field
            LegUnderlyingId = ReadUInt32(bytes, 208),      // NEW: Multi-leg field

            // int16/uint16 fields (offsets 212-223)
            ApplId = ReadInt16(bytes, 212),
            MaturityYear = ReadUInt16(bytes, 214),
            DecayStartDate = ReadUInt16(bytes, 216),
            ChannelId = ReadUInt16(bytes, 218),
            LegCount = ReadUInt16(bytes, 220),             // NEW: Multi-leg field
            LegIndex = ReadUInt16(bytes, 222),             // NEW: Multi-leg field

            // String fields (offsets 224-486) - ALL FIXED
            Currency = ReadCString(bytes.Slice(224, 4)),                 // FIXED: was at 178
            SettlCurrency = ReadCString(bytes.Slice(228, 4)),            // FIXED: was at 183
            SecSubType = ReadCString(bytes.Slice(232, 6)),               // FIXED: was at 188
            RawSymbol = ReadCString(bytes.Slice(238, 71)),               // FIXED: was at 194, len 22!
            Group = ReadCString(bytes.Slice(309, 21)),                   // FIXED: was at 216
            Exchange = ReadCString(bytes.Slice(330, 5)),                 // FIXED: was at 237
            Asset = ReadCString(bytes.Slice(335, 11)),                   // FIXED: was at 242, len 7!
            Cfi = ReadCString(bytes.Slice(346, 7)),                      // FIXED: was at 249
            SecurityType = ReadCString(bytes.Slice(353, 7)),             // FIXED: was at 256
            UnitOfMeasure = ReadCString(bytes.Slice(360, 31)),           // FIXED: was at 263
            Underlying = ReadCString(bytes.Slice(391, 21)),              // FIXED: was at 294
            StrikePriceCurrency = ReadCString(bytes.Slice(412, 4)),      // NEW
            LegRawSymbol = ReadCString(bytes.Slice(416, 71)),            // NEW: Multi-leg field

            // Enum/byte fields (offsets 487-502) - ALL FIXED
            InstrumentClass = (InstrumentClass)bytes[487],               // FIXED: was at 319!
            MatchAlgorithm = (MatchAlgorithm)bytes[488],                 // FIXED: was at 328!
            MainFraction = bytes[489],
            PriceDisplayFormat = bytes[490],
            SubFraction = bytes[491],
            UnderlyingProduct = bytes[492],
            SecurityUpdateAction = (SecurityUpdateAction)bytes[493],
            MaturityMonth = bytes[494],
            MaturityDay = bytes[495],
            MaturityWeek = bytes[496],
            UserDefinedInstrument = (UserDefinedInstrument)bytes[497],
            ContractMultiplierUnit = (sbyte)bytes[498],
            FlowScheduleType = (sbyte)bytes[499],
            TickRule = bytes[500],
            LegInstrumentClass = (InstrumentClass)bytes[501],            // NEW: Multi-leg field
            LegSide = (Side)bytes[502]                                   // NEW: Multi-leg field
            // Reserved bytes 503-519 ignored
        };
    }

    private static ImbalanceMessage DeserializeImbalanceMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 112;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid ImbalanceMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 320;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid ErrorMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
            Code = (ErrorCode)code,
            IsLast = isLast
        };
    }

    private static SymbolMappingMessage DeserializeSymbolMappingMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 176;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid SymbolMappingMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 320;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid SystemMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

        string message = ReadCString(bytes.Slice(16, 303));
        byte code = bytes[319];

        return new SystemMessage
        {
            RType = rtype,
            PublisherId = publisherId,
            InstrumentId = instrumentId,
            TimestampNs = tsEvent,
            Message = message,
            Code = (SystemCode)code
        };
    }

    private static StatMessage DeserializeStatMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 80;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid StatMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 80;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid BboMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 80;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid CbboMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 80;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid Cmbp1Msg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

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

    private static TcbboMessage DeserializeTcbboMsg(ReadOnlySpan<byte> bytes, byte rtype,
        ushort publisherId, uint instrumentId, long tsEvent)
    {
        // MEDIUM FIX: Exact size check instead of minimum
        const int ExpectedSize = 80;
        if (bytes.Length != ExpectedSize)
            throw new ArgumentException($"Invalid TcbboMsg size: expected {ExpectedSize}, got {bytes.Length}", nameof(bytes));

        // TcbboMsg has the same structure as Cmbp1Msg but represents trade with consolidated BBO
        long price = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(24, 4));
        Action action = (Action)bytes[28];
        Side side = (Side)bytes[29];
        byte flags = bytes[30];
        long tsRecv = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32, 8));
        int tsInDelta = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(40, 4));
        ConsolidatedBidAskPair level = DeserializeConsolidatedBidAskPair(bytes.Slice(48, 32));

        return new TcbboMessage
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

    // Helper methods for reading integers from byte arrays
    private static short ReadInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(offset, 2));
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static long ReadInt64(ReadOnlySpan<byte> bytes, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(offset, 8));
    }

    private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, int offset)
    {
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, 8));
    }

    /// <summary>
    /// Reads RawInstrumentId from bytes with overflow protection.
    /// DBN spec defines this as 64-bit, but we use 32-bit uint for backward compatibility.
    /// Throws if the value exceeds uint.MaxValue (4,294,967,295).
    /// </summary>
    private static uint ReadRawInstrumentId(ReadOnlySpan<byte> bytes, int offset)
    {
        // Read full 64-bit value as per DBN specification
        ulong rawId64 = ReadUInt64(bytes, offset);

        // Check for overflow
        if (rawId64 > uint.MaxValue)
        {
            throw new OverflowException(
                $"RawInstrumentId value {rawId64:N0} exceeds uint.MaxValue ({uint.MaxValue:N0}). " +
                $"This venue requires 64-bit instrument IDs. Please contact support or file an issue at " +
                $"https://github.com/databento/databento-dotnet/issues to request ulong support.");
        }

        return (uint)rawId64;
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

#include <databento/historical.hpp>
#include <databento/exceptions.hpp>
#include <iostream>

int main() {
    try {
        auto client = databento::HistoricalBuilder().SetKeyFromEnv().Build();

        std::string dataset = "GLBX.MDP3";
        std::vector<std::string> symbols = {"CLZ5"};
        databento::Schema schema = databento::Schema::Ohlcv1D;

        // Future dates - May 1, 2025 to Nov 18, 2025
        databento::DateTimeRange<databento::UnixNanos> date_range{
            databento::UnixNanos{1746057600000000000LL},  // 2025-05-01
            databento::UnixNanos{1763884800000000000LL}   // 2025-11-18
        };

        std::cout << "Querying CLZ5 with future dates..." << std::endl;

        int count = 0;
        client.TimeseriesGetRange(
            dataset,
            date_range,
            symbols,
            schema,
            [&count](const databento::Record& record) {
                count++;
                std::cout << "Record received" << std::endl;
                return databento::KeepGoing::Continue;
            }
        );

        std::cout << "SUCCESS: Received " << count << " records" << std::endl;
        return 0;

    } catch (const std::exception& e) {
        std::cout << "CAUGHT EXCEPTION: " << e.what() << std::endl;
        return 1;
    }
}

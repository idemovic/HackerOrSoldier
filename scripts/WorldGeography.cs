using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// The world map's picture: simplified continent outlines as lon/lat
// polygons, rasterized once at native resolution in the house VGA style.
// Ordered dither only, no smooth gradients: shallow ocean fades to deep in
// bands of 75%, 50% and 25% light pixels, land is dithered green with ochre
// deserts, snow-dusted tundra and white ice caps, a 1px dark coastline and a
// faint dotted graticule every 30 degrees.
public static class WorldGeography
{
    private enum Kind : byte { Ocean, Land, Ice }

    private record Shape(Kind Kind, Vector2[] Points);

    private static Vector2[] P(params double[] lonLat)
    {
        var points = new Vector2[lonLat.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector2((float)lonLat[2 * i], (float)lonLat[2 * i + 1]);
        return points;
    }

    private static readonly Shape[] Shapes =
    {
        // North America
        new(Kind.Land, P(
            -168, 65.7, -166.5, 68.3, -163, 69.7, -156.5, 71.3, -150, 70.5, -143, 70, -141, 69.6, -135, 69,
            -130, 70, -125, 69.7, -117, 68.8, -112, 68.3, -108, 68.2, -100, 67.8, -98, 70, -95, 71.5,
            -90, 69.5, -85.5, 69.8, -82, 69, -86, 66.5, -87.5, 64.5, -90, 63.5, -93, 61.5, -94.5, 59,
            -93, 57, -90, 57, -85, 55.5, -82.3, 53, -80, 51.5, -79, 52.5, -79, 54.8, -77.5, 56, -77, 58.5,
            -78, 60.5, -77.5, 62.4, -73.5, 62.3, -70, 61, -69.5, 59, -64, 60.5, -62, 57.5, -60, 55.5,
            -57.5, 53.8, -56, 52, -59, 50.3, -64, 50.2, -67.5, 49.3, -64.5, 48.2, -64.7, 46.5, -61, 45.8,
            -60, 46, -61, 45, -63.5, 44.5, -66, 43.6, -66.2, 44.5, -67, 44.8, -68.5, 44.2, -70, 43.7,
            -70.7, 42.7, -70, 41.7, -71.5, 41.4, -74, 40.6, -74, 39.5, -75.5, 38.5, -76, 37, -75.7, 35.2,
            -77, 34.5, -79, 33.5, -81, 31.8, -81.3, 30, -80.1, 27, -80.3, 25.3, -81.2, 25.2, -82, 26.7,
            -82.8, 28, -84, 30, -85.5, 29.7, -87.5, 30.3, -89, 30.2, -89.4, 29, -91, 29.2, -93.5, 29.7,
            -95, 29.2, -97, 27.8, -97.4, 25.5, -97.7, 22.5, -96.5, 19.5, -95, 18.5, -93, 18.4, -91.5, 18.6,
            -90.7, 19.5, -90.4, 21, -88, 21.5, -86.8, 21, -87.5, 19, -88, 16.5, -88.5, 15.8, -86, 15.9,
            -84, 15.7, -83.4, 15, -83.5, 13, -83.8, 11, -82.5, 9.7, -81, 9, -79.5, 9.6, -77.9, 8.8, -77.3, 8.3,
            -78.5, 7.5, -79.5, 8.8, -80.5, 7.5, -82, 8.2, -83, 8.3, -85.7, 10, -85.7, 11, -87.5, 12.9,
            -89, 13.3, -91.5, 14, -94, 15.8, -96.5, 15.7, -99, 16.7, -101.5, 17.7, -104, 19, -105.5, 20.5,
            -105.5, 22.5, -107, 24.5, -109, 26, -111, 28, -112.5, 29.8, -114.7, 31.7, -113.5, 29, -112.2, 27,
            -111, 25.5, -110, 24, -109.5, 23, -110.5, 23.2, -112, 24.5, -114, 27, -115.5, 29, -116.7, 31.5,
            -117.2, 32.7, -118.5, 34, -120.5, 34.5, -121.9, 36.6, -122.5, 37.8, -123.8, 39.5, -124.3, 40.5,
            -124, 43, -124.1, 46.2, -123.8, 48.4, -124.5, 48.5, -127, 50.5, -128.8, 52.8, -130.5, 54.7,
            -132.5, 56.5, -135, 58.5, -137.5, 58.8, -140, 59.7, -144, 60, -147, 60.7, -150, 59.5, -152, 58.2,
            -154, 57.5, -157, 56.8, -160, 55.7, -163, 54.8, -164.5, 54.5, -162, 56, -158.5, 57.5, -157.5, 58.7,
            -161, 58.7, -162, 60, -165, 60.7, -165.5, 62.5, -163, 63.2, -161, 64.5, -163.5, 64.6, -166, 64.7)),
        // Canadian arctic islands, Newfoundland, the Caribbean
        new(Kind.Land, P(-80, 73.5, -72, 72, -67, 69.5, -62, 66.5, -64, 63.5, -67, 62.3, -72, 63.5, -78, 65,
            -74, 68, -80, 70.5, -90, 72.5)),
        new(Kind.Land, P(-118, 72, -105, 73, -100, 70.5, -105, 68.8, -115, 69)),
        new(Kind.Land, P(-90, 76.5, -80, 76, -75, 78, -70, 80.5, -85, 82, -92, 78)),
        new(Kind.Land, P(-59, 47.6, -56, 51.5, -53, 49.5, -52.7, 47, -55, 47)),
        new(Kind.Land, P(-84.9, 21.9, -82, 23.1, -79, 22.5, -74.2, 20.2, -77.5, 19.9, -80, 21.6)),
        new(Kind.Land, P(-74.4, 18.5, -72, 19.9, -68.4, 18.6, -71, 17.8)),
        // Greenland
        new(Kind.Ice, P(-73, 78, -66, 80.5, -60, 82.5, -30, 83.8, -20, 81.5, -18, 77, -20, 74, -22, 70,
            -26, 68, -33, 66, -40, 65, -43, 60, -48, 61, -52, 64.5, -54, 68, -58, 75, -66, 76)),
        // South America
        new(Kind.Land, P(
            -77.5, 8.5, -75.5, 10.5, -72, 12, -71.5, 11, -68, 10.6, -64, 10.5, -61, 10.5, -60, 8.5, -57, 6,
            -52, 5, -50, 1.8, -50, 0, -48, -1, -44, -2.5, -40, -3, -35, -5.5, -35, -9, -37.5, -12.5,
            -39, -17, -40, -22, -43, -23, -45, -24, -48.5, -26, -48.5, -28.5, -52, -32, -53.5, -34,
            -57, -35, -57, -37, -62, -39, -65, -41, -63.5, -42.5, -65.5, -45, -67.5, -46.5, -66, -48,
            -69, -51, -68.5, -52.5, -71, -54, -68, -55, -72, -54, -74, -52, -75, -47, -74, -43,
            -73.5, -37, -71.5, -32, -71.5, -28, -70, -18.5, -75, -15, -78, -11, -81, -6, -81, -4,
            -80, -2, -80.5, 0, -78.5, 2, -77.5, 4, -77.5, 7)),
        // Eurasia, from Gibraltar east along the Mediterranean shore, so the
        // sea is the gap between its outline and Africa's.
        new(Kind.Land, P(
            -5.6, 36, -4.5, 36.7, -2, 36.8, -0.5, 38.3, 0, 39, 0.2, 40.2, 1, 41.1, 3.2, 41.9, 3, 43.2,
            5, 43.3, 6.5, 43.1, 7.5, 43.8, 8.8, 44.4, 9.8, 44.2, 10.5, 43, 11.1, 42.3, 12.3, 41.5,
            13.5, 41.2, 14.8, 40.7, 15.7, 40, 16, 39, 15.7, 38, 16.1, 38, 17, 39, 17.1, 40.5, 18.5, 40.1,
            17, 41, 15.8, 41.9, 14, 42.5, 13.5, 43.6, 12.5, 44.3, 12.3, 45.2, 13.6, 45.7, 14.5, 45.2,
            15.5, 44, 17, 43, 18.5, 42.2, 19.4, 41.9, 19.5, 40.5, 20.2, 39.6, 20.8, 38.6, 21.5, 37.9,
            21.7, 36.8, 22.5, 36.4, 23, 37, 24, 38, 23.2, 39, 22.8, 40.2, 24, 40.6, 26, 40.8, 26.3, 40,
            26.2, 39.5, 27, 38.5, 27.4, 37, 28.3, 36.7, 30, 36.2, 32, 36.2, 34, 36.2, 36, 36.6, 36, 35,
            35.5, 34, 35, 33, 34.7, 31.8, 34.3, 31.3, 34.9, 29.5, 34.4, 27.8, 35.5, 27.3, 37, 25,
            38.5, 23.5, 39, 21.5, 40.5, 19, 41.5, 17, 42.7, 15.5, 43.2, 13.2, 44, 12.7, 45, 12.8, 47, 13.5,
            48.5, 14, 50, 15, 52, 16, 54, 17, 55.5, 17.8, 56.5, 18.8, 58, 20.2, 59.8, 22.5, 58.5, 23.5,
            57, 24.2, 56.5, 25.5, 56.2, 26.3, 55.3, 25.3, 54, 24.2, 52.5, 24.1, 51.6, 24.3, 51.3, 25.8,
            51, 26.1, 50, 26.5, 49.5, 27.5, 48.5, 28.5, 48, 29.8, 49, 30.2, 50.2, 30, 51, 28.9, 52.5, 27.6,
            54.5, 26.5, 56, 27, 57, 25.7, 59, 25.4, 61.5, 25.1, 63.5, 25.3, 66.5, 25.4, 67.2, 24.8,
            68.5, 23.3, 69, 22.3, 70.5, 20.9, 72.7, 21, 72.9, 19.5, 73.5, 17.5, 74.5, 14.8, 75.5, 12.5,
            76.5, 9.8, 77.3, 8.1, 78, 8.8, 79.2, 10, 79.8, 12, 80.3, 13.5, 80.2, 15.5, 81.5, 16.5,
            82.5, 17, 84.5, 18.7, 86.5, 20, 87, 21.5, 88.5, 21.8, 89.5, 21.8, 91, 22.5, 92, 21, 93.5, 19.5,
            94.5, 17, 95.5, 16, 97.5, 16.5, 97.7, 14.5, 98.6, 12.4, 98.3, 9, 98.5, 8, 99.5, 7.2, 100.4, 6,
            100.3, 5.5, 100.5, 3.5, 101.2, 2.8, 102.5, 1.8, 103.5, 1.3, 104.2, 1.4, 103.5, 2.5, 103.3, 4.5,
            102.3, 6.2, 101, 6.9, 100.3, 6.5, 100.1, 8.5, 99.3, 10, 99.7, 11.5, 100, 13.3, 100.9, 12.7,
            102.4, 12.2, 103.2, 10.8, 104.8, 10.2, 105.1, 8.7, 106.5, 9.5, 107.5, 10.5, 109, 11.8, 109.3, 13.5,
            109, 15.5, 108, 16.3, 106.8, 17.8, 105.8, 19, 106.5, 20.2, 107.8, 21.4, 109.7, 21.6, 110.4, 20.6,
            111, 21.5, 113, 22.2, 114.1, 22.3, 115.5, 22.8, 117, 23.3, 118.5, 24.5, 119.7, 25.5, 120.7, 27,
            121.6, 28.5, 121.8, 30.5, 121.3, 31.7, 120.4, 32.5, 119.5, 34.3, 120.8, 36.3, 122.5, 37.1,
            121, 37.8, 119, 37.2, 117.8, 38.2, 118.2, 39.2, 119.5, 39.8, 121.5, 40.9, 121.2, 39, 122.2, 40,
            123.5, 39.8, 124.5, 40, 125.2, 39.5, 125, 38, 126.5, 37.5, 126.3, 35, 126.5, 34.5, 127.5, 34.8,
            129, 35.1, 129.4, 36.5, 129.3, 37.8, 128.4, 38.6, 127.5, 39.8, 128.7, 40.6, 129.8, 41, 130.7, 42.3,
            132, 43, 133, 42.8, 135, 43.8, 137, 45.5, 139, 47.5, 140.5, 49.5, 140.5, 51.5, 141.3, 53,
            139.5, 54.2, 137.5, 54, 135.5, 54.7, 137, 56, 139, 56.5, 141, 58, 143, 59.3, 147, 59.4, 150, 59.5,
            152, 59, 155, 59.2, 156, 57.5, 155.7, 56, 156, 53, 156.8, 51, 158.5, 52.3, 160, 54, 162, 55.8,
            163.2, 57.5, 162.8, 59, 164, 60.2, 166.5, 60.4, 170, 60, 172, 61, 174, 61.8, 177.5, 62.5,
            179.5, 63.5, 180, 64.7, 180, 68.5, 178, 69, 170, 69.7, 165, 69.5, 160, 69.7, 155, 71, 150, 71.5,
            145, 72.2, 140, 72.6, 135, 71.5, 130.5, 71, 129, 72.5, 127, 73.5, 120, 73, 113, 73.6, 110, 74.5,
            106, 76.5, 104, 77.7, 100, 76.5, 97, 76, 93, 75.7, 88, 75.5, 86, 74.5, 82, 73.5, 80.5, 72.5,
            77, 72.8, 73, 72.8, 70, 72.5, 68, 71, 67, 69.5, 66.5, 67.5, 64, 68.5, 60.5, 69, 58, 68.5,
            55, 68.5, 53, 68.8, 49, 68, 46, 67.7, 44, 68.3, 41, 67.1, 40, 66.2, 37, 66.4, 34.5, 66.6,
            33, 66.8, 34, 68.2, 31, 69.7, 28, 70.3, 25, 71, 23, 70.5, 20, 70, 17, 69, 15, 68.2, 14, 67,
            13, 65.8, 11.5, 64.5, 10.5, 63.5, 8.5, 63, 6, 62, 5, 61, 5, 59.5, 5.8, 58.7, 7, 58, 8.5, 58.2,
            9.8, 59, 10.7, 59.8, 11.5, 59, 11.2, 58.2, 11.8, 57.5, 12.5, 56.5, 12.8, 55.5, 14.2, 55.4,
            15.5, 56.2, 16.4, 57.2, 16.7, 58.5, 18, 59.4, 17.5, 60.7, 17.2, 62.3, 18.5, 63.5, 21, 64.5,
            22.5, 65.7, 25, 65.5, 25.3, 64.8, 24.3, 64, 22.5, 63, 21.3, 61.6, 21.5, 60.8, 22.5, 60.2, 24, 59.9,
            27, 60.4, 29, 60.5, 30, 60, 28.5, 59.4, 26, 59.5, 24, 59.3, 23.4, 58.4, 24.3, 57.9, 23.5, 57,
            21.5, 57.3, 21.1, 56.5, 21, 55.4, 19.8, 54.5, 18.5, 54.7, 16.8, 54.4, 15, 54, 13.5, 54.2, 12, 54.2,
            10.9, 54, 10.2, 54.5, 9.8, 55, 10.4, 56.2, 10.6, 57.2, 10.3, 57.7, 9, 57, 8.2, 56.6, 8.1, 55.5,
            8.6, 54.9, 8.8, 54, 8.5, 53.6, 7.2, 53.5, 5.2, 53.3, 4.7, 52.8, 4, 52, 3.5, 51.4, 2.5, 51.1,
            1.7, 50.9, 1.5, 50.1, 0.1, 49.7, -1.2, 49.7, -1.5, 48.6, -3, 48.8, -4.7, 48.5, -4.4, 47.8,
            -2.7, 47.3, -1.8, 46.6, -1.2, 46, -1.3, 44.6, -1.8, 43.4, -3.5, 43.5, -5.5, 43.6, -8, 43.7,
            -9.3, 43.1, -8.9, 42, -8.8, 41, -9.4, 38.9, -8.9, 37, -7.4, 37.2, -6.3, 36.8)),
        // Chukotka, across the date line
        new(Kind.Land, P(-180, 69, -177, 68.4, -172, 66.8, -170, 66, -172, 64.6, -176, 64.6, -180, 64.9)),
        // Africa
        new(Kind.Land, P(
            -5.9, 35.8, -2, 35.1, 3, 36.8, 10, 37.3, 11, 37, 10.3, 34.5, 11, 33, 15, 32.3, 20, 32.2,
            20, 30.8, 25, 31.7, 29, 31, 32.3, 31.2, 32.6, 29.9, 33.7, 27.3, 35.5, 24, 37.2, 21, 37.5, 18.5,
            38.8, 16, 40.5, 14.8, 42.5, 13, 43.3, 11.8, 44, 10.4, 51.2, 11.8, 51, 10.5, 48, 8, 46, 5,
            43.5, 1.5, 41, -2, 39.5, -5, 39, -8, 40.5, -11, 40.5, -14.5, 35.5, -17, 35, -20, 35.5, -24,
            33, -25.8, 32.8, -28.5, 31, -30, 28, -33, 25, -34, 22, -34, 20, -34.8, 18.5, -34, 18, -31,
            16.5, -28.5, 15, -26, 14.5, -22, 12, -18, 13.5, -12, 12, -6, 9, -1, 9.5, 3, 8.5, 4.5, 5, 5.5,
            2.5, 6.3, -1, 5, -4, 5.2, -7.5, 4.4, -11, 6.5, -13.2, 8.5, -15, 11, -16.8, 13.5, -17.5, 14.7,
            -16.5, 19, -17, 21, -15, 25, -13, 27.7, -10, 29.5, -9.7, 31, -8.5, 33.6, -6.8, 34)),
        new(Kind.Land, P(49.3, -12, 50.5, -15.5, 49.5, -18, 47.5, -24.5, 45, -25.5, 43.5, -23, 44, -19.5,
            46.5, -15.8, 48, -14)),
        new(Kind.Land, P(80, 9.5, 81.8, 7.5, 80.5, 6, 79.8, 8)),
        // Europe's islands
        new(Kind.Land, P(-5.7, 50, -3, 50.5, 1.4, 51.2, 1.7, 52.7, 0.2, 53.5, -0.2, 54.5, -1.5, 55.5, -2, 56.5,
            -1.8, 57.5, -4, 57.7, -3, 58.6, -5, 58.6, -6, 57.5, -5.5, 56, -5, 55, -3, 54.8, -3.2, 53.4,
            -4.6, 53.3, -4.1, 52.4, -5.2, 51.8, -3.2, 51.4, -4.2, 51.2)),
        new(Kind.Land, P(-10, 51.7, -6, 52, -6, 54, -5.5, 54.6, -7.5, 55.2, -10, 54)),
        new(Kind.Land, P(-24, 65.5, -22, 66.4, -16, 66.5, -13.5, 65, -15, 64, -19, 63.4, -22.5, 63.8)),
        new(Kind.Ice, P(11, 79.5, 17, 80, 25, 79.5, 18, 77, 14, 77.5)),
        new(Kind.Ice, P(52, 71, 56, 74, 60, 76.5, 68, 77, 60, 75, 57, 72, 55, 70.7)),
        new(Kind.Land, P(12.5, 38, 15.5, 38.2, 15, 36.7)),
        new(Kind.Land, P(8.2, 41, 9.7, 41, 9.5, 39, 8.4, 39)),
        // East Asia
        new(Kind.Land, P(130.8, 34, 132, 35.5, 135.5, 35.7, 136.8, 37.3, 139, 38, 140, 40.5, 141.5, 41, 141.8, 39,
            141, 37, 140.8, 35.7, 139.8, 35, 137, 34.6, 135.5, 33.5, 134.5, 34, 132.5, 34.2)),
        new(Kind.Land, P(140, 42, 140.5, 43.2, 141.5, 45.3, 143, 44.2, 145.5, 43.3, 143.5, 42, 141.5, 42.5)),
        new(Kind.Land, P(129.8, 33, 131, 34, 132, 33, 131.2, 31.4, 130.2, 31.2)),
        new(Kind.Land, P(142, 46, 143.2, 49, 143.5, 54, 142.5, 54, 142, 50)),
        new(Kind.Land, P(120.2, 23, 121.5, 25.2, 122, 25, 121, 22)),
        new(Kind.Land, P(120.5, 18.5, 122.2, 18.3, 122, 16, 121.5, 14, 123.5, 13.2, 120.7, 14, 120, 16)),
        new(Kind.Land, P(122, 7, 123.5, 8.8, 126.5, 9, 126.5, 6.5, 125, 5.8, 123.5, 7.5)),
        // Indonesia and Oceania
        new(Kind.Land, P(95.3, 5.6, 98, 4, 100.5, 2, 103.5, -0.5, 106, -3, 105.8, -5.8, 104.5, -5.8, 102, -4,
            100, -1, 98.5, 2, 96, 4)),
        new(Kind.Land, P(105.2, -6.8, 108, -6.5, 111, -6.5, 114.5, -7.8, 114.4, -8.7, 110, -8.2, 106, -7.4)),
        new(Kind.Land, P(109, 1.5, 110, 2, 111.5, 1.5, 113, 3.2, 115.5, 5, 117, 7, 119, 5.2, 117.8, 4, 118, 1,
            117.5, -1, 116.4, -3.8, 114.5, -4, 111.5, -3, 110, -2.7, 109, -0.5)),
        new(Kind.Land, P(119.5, -5.5, 119, -3, 119.8, 0.5, 121, 1.2, 125, 1.6, 123.2, 0.4, 121.5, -1, 123.2, -1.2,
            123, -4, 121.5, -4.8, 120.5, -3.5, 120.5, -5.5)),
        new(Kind.Land, P(131, -0.8, 134, -0.8, 138, -1.7, 141, -2.6, 145, -4.2, 147.5, -6, 147.8, -8, 150.5, -10.5,
            147, -10, 143.5, -8.8, 141, -9.2, 138, -8.4, 138, -7, 135, -4.2, 132.5, -3.5, 131, -1.5)),
        new(Kind.Land, P(
            113.5, -22, 114, -26, 115, -34, 118, -35, 123, -34, 126, -32.3, 131.5, -31.5, 134, -32.8, 135.5, -34.8,
            137.5, -33, 138, -35.5, 139.5, -36, 140.5, -38, 144, -38.5, 146.5, -39, 148, -37.8, 150, -37, 151, -34,
            153, -31, 153.5, -28, 153, -25.5, 150.5, -22.5, 148.5, -20, 146, -18.5, 145.3, -15, 143.5, -14,
            142.5, -10.8, 141.5, -13, 141.5, -17, 139.5, -17.5, 137, -15.8, 136, -12.2, 132.5, -11.5, 130.5, -12.5,
            129, -15, 125, -14.5, 122.5, -17, 121, -19.5, 117, -20.7)),
        new(Kind.Land, P(144.7, -40.7, 148.3, -40.9, 148, -43, 146.2, -43.7)),
        new(Kind.Land, P(172.7, -34.5, 174.5, -36.5, 175.5, -37, 178.5, -37.7, 176.5, -39.8, 175, -41.5,
            174.8, -39.5, 173.8, -39.2, 174.8, -37)),
        new(Kind.Land, P(172.7, -40.5, 174.3, -41.3, 173, -43.5, 171, -44.5, 170.5, -46, 166.8, -46, 168, -44,
            171.5, -41.7)),
        // Antarctica: the Peninsula, the Ronne and Ross ice shelves, then the map's bottom edge.
        new(Kind.Ice, P(
            -180, -90, -180, -78.3, -168, -78.2, -160, -77.8, -155, -78.2, -152, -76.5, -146, -75.5, -135, -74.5,
            -125, -73.5, -115, -74, -105, -74.5, -100, -72.5, -95, -72, -85, -73, -78, -73.5, -72, -71, -70, -68,
            -67, -66, -64.5, -65, -62, -64, -59, -63.2, -57, -63.3, -60, -64.5, -62, -66, -60, -68.5, -62, -70,
            -61, -72, -62, -73, -58, -73.8, -50, -75.5, -42, -77, -35, -77.5, -30, -75, -20, -72, -10, -71,
            0, -70, 10, -70, 20, -70, 30, -69.8, 40, -68.5, 50, -67.8, 60, -67.5, 69, -69, 75, -69.5, 80, -67.5,
            90, -66.5, 100, -66.5, 110, -66.5, 120, -66.8, 130, -66.3, 140, -66.7, 150, -68.5, 160, -70.5,
            165, -72, 166, -77, 172, -78.3, 180, -78.3, 180, -90)),
    };

    // Inland seas drawn over the land as water.
    private static readonly Vector2[][] Lakes =
    {
        P(28, 41.2, 31.5, 41.1, 35, 42, 38, 41, 41.5, 41.5, 41.5, 43, 37.5, 44.8, 36.5, 45.3, 34.5, 44.7,
            33, 45.2, 33.5, 46.2, 31.5, 46.6, 30, 45.3, 28.7, 44, 28, 42.8),
        P(47, 45, 49, 46.5, 53, 46.5, 53, 44, 51, 43, 51.5, 41, 53, 40.5, 54, 37.5, 50.5, 37, 49, 38.5,
            49, 40.5, 47.5, 42.5),
    };

    // lon0, lat0, lon1, lat1 boxes of desert and of rainforest.
    private static readonly float[][] Deserts =
    {
        new[] { -16f, 15f, 35f, 29f }, new[] { 35f, 14f, 58f, 31f }, new[] { 45f, 26f, 66f, 37f },
        new[] { 84f, 37f, 112f, 46f }, new[] { 115f, -32f, 142f, -19f }, new[] { 14f, -30f, 25f, -18f },
        new[] { -116f, 30f, -104f, 38f }, new[] { 67f, 24f, 74f, 30f }, new[] { -72f, -42f, -64f, -30f },
    };

    private static readonly float[][] Forests =
    {
        new[] { -76f, -12f, -48f, 4f }, new[] { 10f, -6f, 30f, 6f }, new[] { 95f, -8f, 120f, 6f },
        new[] { 98f, 8f, 108f, 20f },
    };

    private static readonly int[,] Bayer4 =
    {
        { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 },
    };

    private static readonly Color Deep = new("0e1f66");
    private static readonly Color Shallow = new("2447a8");
    private static readonly Color Grass = new("3fae4e");
    private static readonly Color GrassDark = new("1d6e2c");
    private static readonly Color Sand = new("d9b070");
    private static readonly Color SandDark = new("8a5a2b");
    private static readonly Color Snow = new("f2f2f2");
    private static readonly Color IceBlue = new("45d6d6");
    private static readonly Color IceEdge = new("6f93b3");

    private static Kind[] _kinds;

    // The land and ice, as polygons filled once at the map's own size.
    private static Kind[] Kinds(int width, int height)
    {
        if (_kinds != null)
            return _kinds;
        var kinds = new Kind[width * height];
        foreach (var shape in Shapes)
            Fill(kinds, width, height, shape.Points, shape.Kind);
        foreach (var lake in Lakes)
            Fill(kinds, width, height, lake, Kind.Ocean);
        return _kinds = kinds;
    }

    // Whether a map pixel of the 640x360 map is land or ice.
    public static bool IsLand(int x, int y) =>
        x >= 0 && y >= 0 && x < WorldMap.Width && y < WorldMap.Height
        && Kinds(WorldMap.Width, WorldMap.Height)[y * WorldMap.Width + x] != Kind.Ocean;

    public static Image Render(int width, int height)
    {
        var kinds = Kinds(width, height);

        var coastDistance = DistanceFromLand(kinds, width, height, 10);
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var kind = kinds[y * width + x];
                float bayer = (Bayer4[y & 3, x & 3] + 0.5f) / 16f;
                var color = kind == Kind.Ocean
                    ? OceanColor(coastDistance[y * width + x], bayer)
                    : LandColor(kind, x, y, width, height, bayer);
                if (kind != Kind.Ocean && OnCoast(kinds, width, height, x, y))
                    color = kind == Kind.Ice ? IceEdge : color.Darkened(0.5f);
                img.SetPixel(x, y, color);
            }
        DrawGraticule(img);
        return img;
    }

    private static Color OceanColor(int distance, float bayer)
    {
        float lit = distance <= 2 ? 0.75f : distance <= 5 ? 0.5f : distance <= 9 ? 0.25f : 0f;
        return bayer < lit ? Shallow : Deep;
    }

    private static Color LandColor(Kind kind, int x, int y, int width, int height, float bayer)
    {
        if (kind == Kind.Ice)
            return bayer < 0.2f ? IceBlue : Snow;

        float lon = (x + 0.5f) / width * 360f - 180f;
        float lat = WorldMap.LatTop - (y + 0.5f) / height * (WorldMap.LatTop - WorldMap.LatBottom);
        float otherBayer = (Bayer4[(y + 2) & 3, (x + 1) & 3] + 0.5f) / 16f;
        if (bayer < BoxDensity(Deserts, lon, lat))
            return otherBayer < 0.3f ? SandDark : Sand;
        float snow = lat > 70f ? 0.6f : lat > 62f ? 0.25f : 0f;
        if (bayer < snow)
            return Snow;
        float dark = BoxDensity(Forests, lon, lat) > 0f ? 0.7f : 0.28f;
        return otherBayer < dark ? GrassDark : Grass;
    }

    // 0 outside every box, rising to 1 four degrees in, so the edge dithers.
    private static float BoxDensity(float[][] boxes, float lon, float lat)
    {
        float best = 0f;
        foreach (var b in boxes)
        {
            float inset = Mathf.Min(Mathf.Min(lon - b[0], b[2] - lon), Mathf.Min(lat - b[1], b[3] - lat));
            best = Mathf.Max(best, Mathf.Clamp(inset / 4f, 0f, 1f));
        }
        return best;
    }

    private static bool OnCoast(Kind[] kinds, int width, int height, int x, int y) =>
        IsOcean(kinds, width, height, x - 1, y) || IsOcean(kinds, width, height, x + 1, y)
        || IsOcean(kinds, width, height, x, y - 1) || IsOcean(kinds, width, height, x, y + 1);

    private static bool IsOcean(Kind[] kinds, int width, int height, int x, int y) =>
        x >= 0 && y >= 0 && x < width && y < height && kinds[y * width + x] == Kind.Ocean;

    // Chebyshev distance to the nearest land pixel, capped, by a two-pass chamfer sweep.
    private static int[] DistanceFromLand(Kind[] kinds, int width, int height, int cap)
    {
        var dist = new int[width * height];
        for (int i = 0; i < dist.Length; i++)
            dist[i] = kinds[i] == Kind.Ocean ? cap : 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                dist[y * width + x] = Mathf.Min(dist[y * width + x], Nearest(dist, width, height, x, y, forward: true) + 1);
        for (int y = height - 1; y >= 0; y--)
            for (int x = width - 1; x >= 0; x--)
                dist[y * width + x] = Mathf.Min(dist[y * width + x], Nearest(dist, width, height, x, y, forward: false) + 1);
        return dist;
    }

    // The smallest distance among the neighbors a sweep has already visited.
    private static int Nearest(int[] dist, int width, int height, int x, int y, bool forward)
    {
        int best = int.MaxValue / 2;
        for (int dy = forward ? -1 : 0; dy <= (forward ? 0 : 1); dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dy == 0 && (forward ? dx >= 0 : dx <= 0))
                    continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                    best = Mathf.Min(best, dist[ny * width + nx]);
            }
        return best;
    }

    // Even-odd scanline fill at pixel centers.
    private static void Fill(Kind[] kinds, int width, int height, Vector2[] lonLat, Kind kind)
    {
        var px = new Vector2[lonLat.Length];
        for (int i = 0; i < px.Length; i++)
            px[i] = WorldMap.Project(lonLat[i].Y, lonLat[i].X);
        var crossings = new List<float>();
        for (int y = 0; y < height; y++)
        {
            float yc = y + 0.5f;
            crossings.Clear();
            for (int i = 0; i < px.Length; i++)
            {
                var a = px[i];
                var b = px[(i + 1) % px.Length];
                if ((a.Y <= yc) != (b.Y <= yc))
                    crossings.Add(a.X + (yc - a.Y) / (b.Y - a.Y) * (b.X - a.X));
            }
            crossings.Sort();
            for (int c = 0; c + 1 < crossings.Count; c += 2)
            {
                int x0 = Mathf.Max(0, Mathf.CeilToInt(crossings[c] - 0.5f));
                int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(crossings[c + 1] - 0.5f) - 1);
                for (int x = x0; x <= x1; x++)
                    kinds[y * width + x] = kind;
            }
        }
    }

    // Dotted lines: every other pixel nudged toward white.
    private static void DrawGraticule(Image img)
    {
        for (int lon = -150; lon <= 150; lon += 30)
        {
            int x = Mathf.RoundToInt(WorldMap.Project(0, lon).X);
            for (int y = 0; y < img.GetHeight(); y += 2)
                Tint(img, x, y);
        }
        for (int lat = -60; lat <= 60; lat += 30)
        {
            int y = Mathf.RoundToInt(WorldMap.Project(lat, 0).Y);
            for (int x = 0; x < img.GetWidth(); x += 2)
                Tint(img, x, y);
        }
    }

    private static void Tint(Image img, int x, int y)
    {
        if (x >= 0 && y >= 0 && x < img.GetWidth() && y < img.GetHeight())
            img.SetPixel(x, y, img.GetPixel(x, y).Lerp(Colors.White, 0.16f));
    }
}

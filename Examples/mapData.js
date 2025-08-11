export const MapTileType = {
    Water: 0,
    Continent: 1,
    Island: 2,
};

export const MapTileBiome = {
    Ocean: 0,
    Coastal: 1,
    Lake: 2,
    Plains: 3,
    Tropical: 4,
    Desert: 5,
    Grassland: 6,
    Tundra: 7,
};

export const MapTileMorph = {
    Flat: 0,
    Rough: 1,
    Mountainous: 2,
    NavRiver: 3,
};

export const MapTileMainFeature = {
    None: 0,
    Volcano: 1,
    MinorRiver: 2,
    Ice: 3,
};

export class MapTile {
    constructor() {
        this.data = 0;
    }

    setType(type) {
        this.data = (this.data & 0xFFFFFFFC) | (type & 0x3);
    }

    getType() {
        return this.data & 0x3;
    }

    setBiome(biome) {
        this.data = (this.data & 0xFFFFFFE3) | ((biome & 0x7) << 2);
    }

    getBiome() {
        return (this.data >> 2) & 0x7;
    }

    setFeature(feature) {
        this.data = (this.data & 0xFFFFFF9F) | ((feature & 0x3) << 5);
    }

    getFeature() {
        return (this.data >> 5) & 0x3;
    }

    setMorph(morph) {
        this.data = (this.data & 0xFFFFFE7F) | ((morph & 0x3) << 7);
    }

    getMorph() {
        return (this.data >> 7) & 0x3;
    }

    setFloodPlain(isFloodPlain) {
        this.data = (this.data & 0xFFFFFDFF) | ((isFloodPlain ? 1 : 0) << 9);
    }

    setWet(isWet) {
        this.data = (this.data & 0xFFFFFBFF) | ((isWet ? 1 : 0) << 10);
    }

    setVegetated(isVegetated) {
        this.data = (this.data & 0xFFFFF7FF) | ((isVegetated ? 1 : 0) << 11);
    }

    setSnow(isSnow) {
        this.data = (this.data & 0xFFFFEFFF) | ((isSnow ? 1 : 0) << 12);
    }

    isSnow() {
        return ((this.data >> 12) & 0x1) !== 0;
    }

    setReef(isReef) {
        this.data = (this.data & 0xFFFFDFFF) | ((isReef ? 1 : 0) << 13);
    }

    setHomeOrDistant(isDistant) {
        this.data = (this.data & 0xFFFFBFFF) | ((isDistant ? 1 : 0) << 14);
    }

    isHomeOrDistant() {
        return ((this.data >> 14) & 0x1) !== 0;
    }

    toFloat() {
        return this.data & 0x1FFF;
    }
}

export class MapData {
    constructor(width, height, playerHome, playerDist, seed) {
        this.width = width;
        this.height = height;
        this.tileCount = width * height;
        this.seed = seed;

        this.tiles = new Array(this.tileCount).fill(null).map(() => new MapTile());

        this.playerHome = playerHome;
        this.playerDistant = playerDist;
        this.totalPlayers = playerHome + playerDist;  
    }
}
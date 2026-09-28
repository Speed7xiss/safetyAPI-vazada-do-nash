const mongoose = require('mongoose');

const userSchema = new mongoose.Schema(
  {
    discordId: { type: String, required: true, unique: true, index: true },

    key: { type: String, default: null },        // licenseKey (raw)
    linkedAt: { type: Date, default: null },

    productLinks: {
      type: [
        {
          product: { type: mongoose.Schema.Types.ObjectId, ref: 'Product', required: true },
          key: { type: String, required: true },
          linkedAt: { type: Date, default: null },
          expiresAt: { type: Date, default: null },
          productHash: { type: String, default: null },
          keyScopeHash: { type: String, default: null },
        },
      ],
      default: [],
    },

    discordUsername: { type: String, default: null },
    discordGlobalName: { type: String, default: null },
    discordTag: { type: String, default: null },
    discordAvatarHash: { type: String, default: null },
    discordAvatarUrl: { type: String, default: null },
    discordFetchedAt: { type: Date, default: null },

    selfHwidFreeResetsUsed: { type: Number, default: 0, min: 0 },

    paused: { type: Boolean, default: false },
    banned: { type: Boolean, default: false },
    banReason: { type: String, default: null },
  },
  { timestamps: true }
);

module.exports = mongoose.model('User', userSchema);

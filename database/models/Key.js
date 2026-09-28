const mongoose = require('mongoose');

const keySchema = new mongoose.Schema(
  {
    prefix: { type: String, required: true, trim: true, uppercase: true },
    code: { type: String, required: true, unique: true, index: true, uppercase: true },
    codeHash: { type: String, default: null, index: true },

    product: { type: mongoose.Schema.Types.ObjectId, ref: 'Product', required: true },
    productHash: { type: String, default: null, index: true },
    createdByAdmin: { type: mongoose.Schema.Types.ObjectId, ref: 'AdminUser', default: null, index: true },

    durationDays: { type: Number, default: null },
    activatedAt: { type: Date, default: null },
    expiresAt: { type: Date, default: null, index: true },

    usedBy: { type: String, default: null },  // discordId (raw)
    usedAt: { type: Date, default: null },
    keyScopeHash: { type: String, default: null, index: true },

    hwid: { type: String, default: null },    // armazenado (hash com salt)
    paused: { type: Boolean, default: false },
    pausedAt: { type: Date, default: null },
    pausedBy: { type: String, default: null },

    banned: { type: Boolean, default: false },
    banReason: { type: String, default: null },
    bannedAt: { type: Date, default: null },
    bannedBy: { type: String, default: null },
  },
  { timestamps: true }
);

module.exports = mongoose.model('Key', keySchema);

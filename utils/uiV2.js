const {
  ContainerBuilder,
  TextDisplayBuilder,
  SeparatorBuilder,
  SeparatorSpacingSize,
} = require('discord.js');

function clampText(s, max = 950) {
  const txt = String(s ?? '')
    .replace(/\u0000/g, '')
    .trim();
  if (!txt) return '—';
  return txt.length > max ? `${txt.slice(0, max)}…` : txt;
}

function v2Card({ title, blocks = [] } = {}) {
  const c = new ContainerBuilder();

  if (title) {
    c.addTextDisplayComponents(
      new TextDisplayBuilder().setContent(clampText(`**${title}**`, 950)),
    );
    if (blocks.length) {
      c.addSeparatorComponents(
        new SeparatorBuilder().setSpacing(SeparatorSpacingSize.Small).setDivider(true),
      );
    }
  }

  for (let i = 0; i < blocks.length; i++) {
    const b = blocks[i];
    if (!b) continue;

    c.addTextDisplayComponents(
      new TextDisplayBuilder().setContent(clampText(b, 950)),
    );

    if (i !== blocks.length - 1) {
      c.addSeparatorComponents(
        new SeparatorBuilder().setSpacing(SeparatorSpacingSize.Small).setDivider(true),
      );
    }
  }

  return c;
}

module.exports = { v2Card };

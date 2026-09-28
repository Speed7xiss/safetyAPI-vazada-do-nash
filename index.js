/**
 * Ponto de entrada principal do bot de Discord
 * @module index
 */
const { Client, GatewayIntentBits, Collection } = require('discord.js');
const fs = require('fs').promises;
const path = require('path');
const mongoose = require('mongoose');
require('dotenv').config();

const { connectDB } = require('./database/connect');
const logger = require('./utils/logger');
const { startApi } = require('./api'); // GARANTA que o arquivo se chama api.js

const client = new Client({
  intents: [
    GatewayIntentBits.Guilds,
    GatewayIntentBits.GuildMessages,
    GatewayIntentBits.MessageContent,
  ],
  allowedMentions: { parse: [] }, // evita abuso de mention
});

client.commands = new Collection();
client.cache = new Map();

// Carregar eventos
const eventsPath = path.join(__dirname, './events');
async function loadEvents() {
  const eventFiles = await fs.readdir(eventsPath);
  for (const file of eventFiles) {
    if (!file.endsWith('.js')) continue;

    const event = require(path.join(eventsPath, file));
    if (!event?.name || typeof event.execute !== 'function') {
      logger.warn(`Evento inválido ignorado: ${file}`);
      continue;
    }

    if (event.once) {
      client.once(event.name, (...args) => event.execute(...args, client));
    } else {
      client.on(event.name, (...args) => event.execute(...args, client));
    }
    logger.info(`Evento ${event.name} carregado com sucesso`);
  }
}

// Carregar comandos
const commandsPath = path.join(__dirname, './commands');
async function loadCommands() {
  const commandFiles = await fs.readdir(commandsPath);
  for (const file of commandFiles) {
    if (!file.endsWith('.js')) continue;

    const command = require(path.join(commandsPath, file));
    if (!command?.data?.name || typeof command.execute !== 'function') {
      logger.warn(`Comando inválido ignorado: ${file}`);
      continue;
    }

    client.commands.set(command.data.name, command);
    logger.info(`Comando /${command.data.name} carregado com sucesso`);
  }
}

async function shutdown(signal, apiServer) {
  try {
    logger.warn(`Recebido ${signal}. Encerrando...`);
    if (apiServer) {
      await new Promise((r) => apiServer.close(() => r()));
    }
    await client.destroy();

    if (mongoose.connection.readyState === 1) {
      await mongoose.connection.close();
    }
  } catch (err) {
    logger.error(`Erro no shutdown: ${err.stack || err.message}`);
  } finally {
    process.exit(0);
  }
}

// Inicializar bot
async function startBot() {
  let apiServer;

  try {
    await connectDB();
    await loadEvents();
    await loadCommands();

    const api = await startApi();
    apiServer = api.server;

    await client.login(process.env.DISCORD_TOKEN);
    logger.info('Bot iniciado com sucesso');
  } catch (error) {
    logger.error(`Erro ao iniciar o bot: ${error.stack || error.message}`);
    process.exit(1);
  }

  process.on('SIGINT', () => shutdown('SIGINT', apiServer));
  process.on('SIGTERM', () => shutdown('SIGTERM', apiServer));

  process.on('unhandledRejection', (reason) => {
    logger.error(`unhandledRejection: ${reason?.stack || reason}`);
  });
  process.on('uncaughtException', (err) => {
    logger.error(`uncaughtException: ${err?.stack || err}`);
  });
}

startBot();

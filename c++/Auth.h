#pragma once
#ifndef AUTH_H
#define AUTH_H

#include <string>
#include <vector>
#include <atomic>
#include <mutex>

static size_t AuthWriteCallback(void* contents, size_t size, size_t nmemb, void* userp) {
    size_t total = size * nmemb;
    ((std::string*)userp)->append((char*)contents, total);
    return total;
}

std::string GenerateHWID();
bool SetProductHash(const std::string& productHash, std::string& error_message);
std::string GetProductHash();
void ClearProductHash();
bool PerformLogin(const std::string& username, const std::string& password, const std::string& hwid, std::string& error_message, const std::string& productHash = "");
void CleanupAuth(); // Nova função para cleanup

#endif
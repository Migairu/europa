/*
* Copyright (C) Migairu Corp.
* Written by Juan Miguel Giraldo.
* 
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
* 
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

const PBKDF2_ITERATIONS = 100000;
const SALT_LENGTH = 16;
const IV_LENGTH = 12;
const CHUNK_SIZE = 5 * 1024 * 1024;

function sanitizeFileName(fileName) {
    let sanitized = fileName.replace(/^.*[\\\/]/, '');
    sanitized = sanitized.replace(/\0/g, '');
    sanitized = sanitized.replace(/[<>:"/\\|?*\x00-\x1F]/g, '_');
    sanitized = sanitized.replace(/[\x00-\x1F\x7F-\x9F]/g, '');
    sanitized = sanitized.replace(/[_\s]+/g, '_');
    sanitized = sanitized.replace(/^[\s._]+|[\s._]+$/g, '');

    if (!sanitized) {
        sanitized = 'unnamed_file';
    }

    if (sanitized.length > 255) {
        const extension = sanitized.lastIndexOf('.') !== -1
            ? sanitized.slice(sanitized.lastIndexOf('.'))
            : '';
        sanitized = sanitized.slice(0, 255 - extension.length) + extension;
    }

    return sanitized;
}

async function deriveKey(passphrase, salt) {
    const encoder = new TextEncoder();
    const passphraseBuffer = encoder.encode(passphrase);
    const keyMaterial = await window.crypto.subtle.importKey(
        "raw",
        passphraseBuffer,
        { name: "PBKDF2" },
        false,
        ["deriveBits", "deriveKey"]
    );
    return await window.crypto.subtle.deriveKey(
        {
            name: "PBKDF2",
            salt: salt,
            iterations: PBKDF2_ITERATIONS,
            hash: "SHA-256"
        },
        keyMaterial,
        { name: "AES-GCM", length: 256 },
        false,
        ["encrypt", "decrypt"]
    );
}

async function decryptFileAndMetadata(encryptedData, iv, salt, passphrase) {
    try {
        const key = await deriveKey(passphrase, salt);

        const encryptedArray = new Uint8Array(encryptedData);

        const decryptedData = await window.crypto.subtle.decrypt(
            {
                name: "AES-GCM",
                iv: iv,
                tagLength: 128
            },
            key,
            encryptedArray
        );

        const decryptedArray = new Uint8Array(decryptedData);

        const delimiter = new Uint8Array([0xFF, 0xFE, 0xFD, 0xFC]);
        let delimiterIndex = findSubarray(decryptedArray, delimiter);

        if (delimiterIndex === -1) {
            throw new Error("Could not find metadata delimiter");
        }

        const decoder = new TextDecoder();
        const metadataString = decoder.decode(decryptedArray.slice(0, delimiterIndex));
        const metadata = JSON.parse(metadataString);

        const fileContent = decryptedArray.slice(delimiterIndex + delimiter.length);

        return { metadata, fileContent };
    } catch (error) {
        console.error('Decryption error:', error);
        throw error;
    }
}

async function prepareFileUpload(file, passphrase) {
    const salt = window.crypto.getRandomValues(new Uint8Array(16));
    const iv = window.crypto.getRandomValues(new Uint8Array(12));
    const key = await deriveKey(passphrase, salt);

    const metadata = {
        fileName: sanitizeFileName(file.name),
        fileType: file.type,
        fileSize: file.size,
        uploadDate: new Date().toISOString()
    };

    const metadataString = JSON.stringify(metadata);
    const metadataEncoder = new TextEncoder();
    const metadataArray = metadataEncoder.encode(metadataString);
    const delimiter = new Uint8Array([0xFF, 0xFE, 0xFD, 0xFC]);

    const totalSize = metadataArray.length + delimiter.length + file.size;
    const totalChunks = Math.ceil(totalSize / CHUNK_SIZE);

    return {
        key,
        iv: Array.from(iv),
        salt: Array.from(salt),
        totalChunks,
        metadataArray,
        delimiter,
        totalSize
    };
}

async function* generateEncryptedChunks(file, key, iv, metadataArray, delimiter) {
    const fileArrayBuffer = await file.arrayBuffer();
    const fileArray = new Uint8Array(fileArrayBuffer);

    const completeData = new Uint8Array(metadataArray.length + delimiter.length + fileArray.length);
    completeData.set(metadataArray, 0);
    completeData.set(delimiter, metadataArray.length);
    completeData.set(fileArray, metadataArray.length + delimiter.length);

    const encryptedData = await window.crypto.subtle.encrypt(
        {
            name: "AES-GCM",
            iv: iv,
            tagLength: 128
        },
        key,
        completeData
    );

    const encryptedArray = new Uint8Array(encryptedData);

    for (let offset = 0; offset < encryptedArray.length; offset += CHUNK_SIZE) {
        const chunk = encryptedArray.slice(offset, Math.min(offset + CHUNK_SIZE, encryptedArray.length));
        yield chunk;
    }
}

function findSubarray(arr, subarr) {
    for (let i = 0; i < arr.length; i++) {
        let found = true;
        for (let j = 0; j < subarr.length; j++) {
            if (arr[i + j] !== subarr[j]) {
                found = false;
                break;
            }
        }
        if (found) return i;
    }
    return -1;
}
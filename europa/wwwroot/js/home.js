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

$(document).ready(function () {
    var contentCache = {};
    const maxTotalFileSize = (2 * 1024 * 1024 * 1024);
    let currentTotalSize = 0;
    let selectedFiles = new DataTransfer();
    function initializeFileUpload() {
        const dropZone = document.getElementById('dropZone');
        const fileInput = document.getElementById('fileInput');
        const fileList = document.getElementById('fileList');

        if (fileInput) {
            fileInput.addEventListener('change', (e) => handleFileSelect(e.target.files));
        }

        if (dropZone) {
            dropZone.addEventListener('click', () => fileInput.click());
            dropZone.addEventListener('dragover', (e) => {
                e.preventDefault();
                dropZone.classList.add('dragover');
            });
            dropZone.addEventListener('dragleave', () => {
                dropZone.classList.remove('dragover');
            });
            dropZone.addEventListener('drop', async (e) => {
                e.preventDefault();
                dropZone.classList.remove('dragover');
                const items = e.dataTransfer.items;
                const files = [];
                for (let item of items) {
                    if (item.kind === 'file') {
                        const entry = item.webkitGetAsEntry();
                        if (entry) {
                            if (entry.isFile) {
                                files.push(await getFileFromEntry(entry));
                            } else if (entry.isDirectory) {
                                files.push(...await getFilesFromDirectory(entry));
                            }
                        }
                    }
                }
                handleFileSelect(files);
            });
        }

        if (fileList) {
            fileList.addEventListener('click', (e) => {
                if (e.target.classList.contains('remove-file')) {
                    const fileName = e.target.dataset.name;
                    const newSelectedFiles = new DataTransfer();

                    for (let i = 0; i < selectedFiles.files.length; i++) {
                        const file = selectedFiles.files[i];
                        if (file.name !== fileName) {
                            newSelectedFiles.items.add(file);
                        } else {
                            currentTotalSize -= file.size;
                        }
                    }

                    selectedFiles = newSelectedFiles;
                    if (fileInput) fileInput.files = selectedFiles.files;
                    e.target.closest('.file-item').remove();
                    updateRemainingSpace();
                }
            });
        }
    }
    async function getFileFromEntry(entry) {
        return new Promise((resolve, reject) => {
            entry.file(resolve, reject);
        });
    }
    async function getFilesFromDirectory(dirEntry) {
        const files = [];
        const dirReader = dirEntry.createReader();

        const readEntries = () => new Promise((resolve, reject) => {
            dirReader.readEntries(resolve, reject);
        });

        let entries;
        do {
            entries = await readEntries();
            for (let entry of entries) {
                if (entry.isFile) {
                    files.push(await getFileFromEntry(entry));
                } else if (entry.isDirectory) {
                    files.push(...await getFilesFromDirectory(entry));
                }
            }
        } while (entries.length > 0);

        return files;
    }
    function updateRemainingSpace() {
        const remainingSpace = document.getElementById('remainingSpace');
        if (remainingSpace) {
            const remainingBytes = maxTotalFileSize - currentTotalSize;
            const remainingGB = (remainingBytes / (1024 * 1024 * 1024)).toFixed(2);
            remainingSpace.textContent = `${remainingGB} GB remaining`;
        }
    }
    function formatFileSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        else if (bytes < 1048576) return (bytes / 1024).toFixed(2) + ' KB';
        else if (bytes < 1073741824) return (bytes / 1048576).toFixed(2) + ' MB';
        else return (bytes / 1073741824).toFixed(2) + ' GB';
    }
    function validateFiles(files) {
        const errors = [];
        for (let file of files) {
            try {
                if (file.size === 0) {
                    errors.push(`File "${file.name}" is empty`);
                    continue;
                }

                if (file.size > maxTotalFileSize) {
                    errors.push(`File "${file.name}" exceeds the size limit of ${formatFileSize(maxTotalFileSize)}`);
                    continue;
                }

                if (file.name.length > 255) {
                    errors.push(`File name "${file.name}" is too long (max 255 characters)`);
                    continue;
                }

                if (/[<>:"/\\|?*\x00-\x1F]/g.test(file.name)) {
                    errors.push(`File name "${file.name}" contains invalid characters`);
                    continue;
                }

                if (file.name.startsWith('.') || file.name.endsWith('.') ||
                    file.name.startsWith(' ') || file.name.endsWith(' ')) {
                    errors.push(`File name "${file.name}" cannot start or end with periods or spaces`);
                    continue;
                }

            } catch (error) {
                errors.push(`Error validating file "${file.name}": ${error.message}`);
            }
        }
        return errors;
    }
    function handleFileSelect(files) {
        const validationErrors = validateFiles(files);

        if (validationErrors.length > 0) {
            validationErrors.forEach(error => showErrorNotification(error));
            return;
        }

        for (let file of files) {
            let isDuplicate = false;
            for (let i = 0; i < selectedFiles.files.length; i++) {
                if (selectedFiles.files[i].name === file.name &&
                    selectedFiles.files[i].size === file.size &&
                    selectedFiles.files[i].lastModified === file.lastModified) {
                    isDuplicate = true;
                    break;
                }
            }

            if (isDuplicate) {
                showErrorNotification(`The file "${file.name}" has already been selected.`);
                continue;
            }

            if ((currentTotalSize + file.size) <= maxTotalFileSize) {
                selectedFiles.items.add(file);
                currentTotalSize += file.size;

                if (fileList) {
                    const fileItem = document.createElement('div');
                    fileItem.className = 'file-item';
                    fileItem.innerHTML = `
                    <span class="file-name">${file.name}</span>
                    <div class="file-item-options">
                        <span class="file-size">${formatFileSize(file.size)}</span>
                        <button type="button" class="remove-file" data-name="${file.name}">×</button>
                    </div>
                `;
                    fileList.appendChild(fileItem);
                }
            } else {
                showErrorNotification('This file exceeds the remaining space and will not be added.');
            }
        }

        if (fileInput) fileInput.files = selectedFiles.files;
        updateRemainingSpace();
    }

    initializeFileUpload();
    $(document).on('submit', '#uploadForm', function (e) {
        e.preventDefault();
        const files = $('#fileInput')[0].files;
        const passphrase = $('#passphraseInput').val();
        let expirationOption;

        expirationOption = $('#expirationSelect').val();

        if (files.length === 0 || !passphrase || !expirationOption) {
            showErrorNotification('Please fill in all required fields.');
            hideLoadingIndicator();
            return;
        }

        showLoadingStage(1);
        updateProgressBar(0);

        const uploadChunkedFile = async (file) => {
            try {
                const uploadPrep = await prepareFileUpload(file, passphrase);
                const { key, iv, salt, totalChunks, metadataArray, delimiter } = uploadPrep;
                const initResponse = await fetch('/api/upload/init', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    },
                    body: JSON.stringify({
                        fileName: file.name,
                        totalChunks: totalChunks,
                        totalSize: file.size,
                        expirationOption: expirationOption,
                        iv: btoa(String.fromCharCode.apply(null, iv)),
                        salt: btoa(String.fromCharCode.apply(null, salt))
                    })
                });

                if (!initResponse.ok) {
                    throw new Error('Failed to initialize upload');
                }

                const { uploadId, fileId } = await initResponse.json();

                let chunkNumber = 0;
                let uploadedBytes = 0;
                const chunkGenerator = generateEncryptedChunks(file, key, new Uint8Array(iv), metadataArray, delimiter);

                showLoadingStage(2);

                for await (const encryptedChunk of chunkGenerator) {
                    const formData = new FormData();
                    formData.append('chunk', new Blob([encryptedChunk]));
                    formData.append('uploadId', uploadId);
                    formData.append('chunkNumber', chunkNumber);

                    const chunkResponse = await fetch('/api/upload/chunk', {
                        method: 'POST',
                        headers: {
                            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                        },
                        body: formData
                    });

                    if (!chunkResponse.ok) {
                        throw new Error(`Failed to upload chunk ${chunkNumber}`);
                    }

                    uploadedBytes += encryptedChunk.byteLength;
                    const progress = (uploadedBytes / file.size) * 100;
                    updateProgressBar(progress);

                    chunkNumber++;
                }

                const finalizeResponse = await fetch('/api/upload/finalize', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    },
                    body: JSON.stringify({
                        uploadId: uploadId,
                        fileId: fileId,
                        fileName: file.name,
                    })
                });

                const result = await finalizeResponse.json();
                showLoadingStage(3);

                setTimeout(() => {
                    const downloadLink = `${result.downloadLink}#${encodeURIComponent(passphrase)}`;
                    $('#downloadLink').val(downloadLink);
                    $('#downloadLinkContainer').show();
                    hideLoadingIndicator();
                }, 1000);

            } catch (error) {
                console.error('Upload error:', error);
                hideLoadingIndicator();
                showErrorNotification('An error occurred during the upload. Please try again.');
            }
        };

        if (files.length === 1) {
            uploadChunkedFile(files[0]).catch(error => {
                hideLoadingIndicator();
                showErrorNotification('An error occurred during the upload. Please try again.');
            });
        } else {
            const zip = new JSZip();
            for (let i = 0; i < files.length; i++) {
                zip.file(files[i].name, files[i]);
            }

            zip.generateAsync({ type: "blob" })
                .then(zipBlob => {
                    const zipFile = new File([zipBlob], "archive.zip");
                    return uploadChunkedFile(zipFile);
                })
                .catch(error => {
                    hideLoadingIndicator();
                    showErrorNotification('An error occurred while preparing the files. Please try again.');
                });
        }
    });
    function showLoadingStage(stage) {
        $('#custom-loading-indicator').fadeIn(300);
        $('.stage').hide();
        $(`.stage-${stage}`).fadeIn(300);
    }

    function hideLoadingIndicator() {
        $('#custom-loading-indicator').fadeOut(300);
    }

    function updateProgressBar(percentage) {
        $('.upload-percentage').text(`${Math.round(percentage)}%`);
    }

    function resetProgressBar() {
        $('.upload-percentage').text('0%');
    }
    function setupEventHandlers() {
        $('#copyButton').on('click', function () {
            const downloadLink = document.getElementById('downloadLink');
            if (downloadLink) {
                const copyButton = document.getElementById('copyButton');
                downloadLink.select();
                document.execCommand('copy');
                copyButton.innerHTML = 'Link copied to clipboard!';
                setTimeout(() => {
                    copyButton.innerHTML = 'Copy link';
                }, 2000);
            }
            
        });
    }

    setupEventHandlers();
    function showErrorNotification(message) {
        const errorNotification = document.getElementById('errorNotification');
        const errorMessage = document.getElementById('errorMessage');

        errorMessage.textContent = message;
        errorNotification.classList.add('show');

        setTimeout(() => {
            errorNotification.classList.remove('show');
        }, 5000);
    }
});
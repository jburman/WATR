async function registerPassKey(optionsJson) {
    const options = JSON.parse(optionsJson);
    
    // Convert base64url to Uint8Array
    options.challenge = coerceToArrayBuffer(options.challenge);
    options.user.id = coerceToArrayBuffer(options.user.id);
    if (options.excludeCredentials) {
        options.excludeCredentials.forEach(c => c.id = coerceToArrayBuffer(c.id));
    }

    const credential = await navigator.credentials.create({ publicKey: options });

    const response = {
        id: credential.id,
        rawId: coerceToBase64Url(credential.rawId),
        type: credential.type,
        extensions: credential.getClientExtensionResults(),
        response: {
            attestationObject: coerceToBase64Url(credential.response.attestationObject),
            clientDataJSON: coerceToBase64Url(credential.response.clientDataJSON),
            transports: credential.response.getTransports ? credential.response.getTransports() : []
        }
    };

    return JSON.stringify(response);
}

async function loginPassKey(optionsJson) {
    const options = JSON.parse(optionsJson);

    options.challenge = coerceToArrayBuffer(options.challenge);
    if (options.allowCredentials) {
        options.allowCredentials.forEach(c => c.id = coerceToArrayBuffer(c.id));
    }

    const assertion = await navigator.credentials.get({ publicKey: options });

    const response = {
        id: assertion.id,
        rawId: coerceToBase64Url(assertion.rawId),
        type: assertion.type,
        extensions: assertion.getClientExtensionResults(),
        response: {
            authenticatorData: coerceToBase64Url(assertion.response.authenticatorData),
            clientDataJSON: coerceToBase64Url(assertion.response.clientDataJSON),
            signature: coerceToBase64Url(assertion.response.signature),
            userHandle: assertion.response.userHandle ? coerceToBase64Url(assertion.response.userHandle) : null
        }
    };

    return JSON.stringify(response);
}

async function performLogin(url, body) {
    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: body
    });

    if (!response.ok) {
        // Return error text if possible
        throw new Error(await response.text());
    }
}

function coerceToArrayBuffer(thing) {
    if (typeof thing === "string") {
        // base64url to base64
        thing = thing.replace(/-/g, "+").replace(/_/g, "/");
        
        // pad base64
        while (thing.length % 4 !== 0) {
            thing += "=";
        }

        const binaryString = window.atob(thing);
        const len = binaryString.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes.buffer;
    }
    return thing;
}

function coerceToBase64Url(thing) {
    const uint8Array = new Uint8Array(thing);
    let binaryString = "";
    for (let i = 0; i < uint8Array.length; i++) {
        binaryString += String.fromCharCode(uint8Array[i]);
    }
    const base64 = window.btoa(binaryString);
    return base64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=/g, "");
}
